using System.Linq.Expressions;
using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Hubs;
using FiloTakip.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Alarmlarla ilgili her şey: yeni alarm üretme, listeleme, okundu işaretleme
public class AlarmService(
    FiloTakipDbContext db,
    IHubContext<LiveHub, ILiveClient> live,
    ILogger<AlarmService> logger)
{
    private const int MaxTake = 200;

    // Alarm -> AlarmDto dönüşümü tek yerde (liste ve tek alarm aynısını kullanır). Expression olduğu için
    // EF Core bunu SQL'e çevirir: plaka ve konum JOIN ile, sadece gereken sütunlar okunur.
    private static readonly Expression<Func<Alarm, AlarmDto>> ToDto = a => new AlarmDto(
        a.AlarmId,
        a.VehicleId,
        a.Vehicle!.Plate,
        a.AlarmType,
        a.Message,
        a.IsRead,
        a.CreatedAt,
        a.Location == null ? null : a.Location.Latitude,
        a.Location == null ? null : a.Location.Longitude);

    // Yeni gelen konum için gereken alarmları ekler (hız, düşük yakıt, yakıt hırsızlığı, rölanti, bölge). KAYDETMEZ: kaydetme işini çağıran yapar,
    // böylece konum ve alarmlar tek SaveChanges ile birlikte kaydedilir (ya hepsi ya hiçbiri).
    // vehicleSpeedLimitKmh: aracın kendi sınırı (yolun sınırı location.RoadSpeedLimitKmh'de).
    // previous: aracın bir önceki konumu (ilk konumuysa null). Eklenen alarmları döner, çoğu zaman boş liste.
    public async Task<List<Alarm>> AddAlarmsIfNeededAsync(
        LocationRecord location,
        short vehicleSpeedLimitKmh,
        LocationRecord? previous,
        CancellationToken cancellationToken)
    {
        var added = new List<Alarm>();
        var now = DateTime.UtcNow;

        // İlk kontrol veritabanına gitmeden yapılır: araç hızlı değilse son alarmı sorgulamaya gerek yok
        // (konumların çoğu bu durumda)
        var speedLimit = AlarmRules.EffectiveSpeedLimit(vehicleSpeedLimitKmh, location.RoadSpeedLimitKmh);
        if (AlarmRules.IsSpeeding(speedLimit, location.SpeedKmh)
            && AlarmRules.ShouldCreateSpeedAlarm(
                speedLimit, previous?.SpeedKmh, location.SpeedKmh,
                await GetLastAlarmAtAsync(location.VehicleId, AlarmTypes.SpeedLimit, cancellationToken), now,
                previous is null ? null : AlarmRules.EffectiveSpeedLimit(vehicleSpeedLimitKmh, previous.RoadSpeedLimitKmh)))
        {
            added.Add(AddAlarm(location, AlarmTypes.SpeedLimit,
                AlarmRules.SpeedViolationMessage(vehicleSpeedLimitKmh, location.RoadSpeedLimitKmh, location.SpeedKmh)));
        }

        // Yakıt bilgisi olmayan cihazlarda (FuelPercent null) yakıt alarmı yok
        if (location.FuelPercent is decimal fuel
            && AlarmRules.IsLowFuel(fuel)
            && AlarmRules.ShouldCreateLowFuelAlarm(
                previous?.FuelPercent, fuel,
                await GetLastAlarmAtAsync(location.VehicleId, AlarmTypes.LowFuel, cancellationToken), now))
        {
            added.Add(AddAlarm(location, AlarmTypes.LowFuel, AlarmRules.LowFuelMessage(fuel)));
        }

        // Yakıt hırsızlığı: sadece araç dururken bakılır (giderken yakıt zaten harcanır, depo da çalkalanır)
        if (location.FuelPercent is decimal currentFuel
            && AlarmRules.IsStationary(location.SpeedKmh)
            && await GetFuelWhenStoppedIfDrainedAsync(location.VehicleId, currentFuel, cancellationToken) is decimal fuelWhenStopped)
        {
            added.Add(AddAlarm(location, AlarmTypes.FuelDrain, AlarmRules.FuelDrainMessage(fuelWhenStopped, currentFuel)));
        }

        // Rölanti: kontak açık ve araç duruyorsa, ne zamandan beri böyle olduğuna bakılır
        if (AlarmRules.IsIdling(location.IgnitionOn, location.SpeedKmh)
            && await GetIdleSinceIfLongAsync(location, cancellationToken) is DateTime idleSince)
        {
            added.Add(AddAlarm(location, AlarmTypes.Idle, AlarmRules.IdleMessage(location.RecordedAt - idleSince)));
        }

        // Bölge: önceki konum dışarıda, bu konum içerideyse araç bölgeye girmiştir (ya da tersi: çıkmıştır).
        // Aracın ilk konumunda karşılaştıracak bir şey yok: bölgenin içinde açılan araç için "girdi" denmez.
        if (previous is not null)
        {
            foreach (var zone in await GetActiveGeofencesAsync(cancellationToken))
            {
                var wasInside = AlarmRules.IsInsideGeofence(zone, previous.Latitude, previous.Longitude);
                var isInside = AlarmRules.IsInsideGeofence(zone, location.Latitude, location.Longitude);
                if (AlarmRules.GeofenceAlarmType(zone, wasInside, isInside) is string alarmType)
                    added.Add(AddAlarm(location, alarmType, AlarmRules.GeofenceMessage(alarmType, zone.Name)));
            }
        }

        return added;
    }

    // Bütün aktif bölgeler. Tablo küçük (birkaç düzine satır), her konumda okumak sorun değil;
    // binlerce araçta bunu önbelleğe (IMemoryCache) almak gerekirdi.
    private Task<List<Geofence>> GetActiveGeofencesAsync(CancellationToken cancellationToken) =>
        db.Geofences
            .AsNoTracking()
            .Where(g => g.IsActive)
            .ToListAsync(cancellationToken);

    // Araç 5 dakikadan uzun süredir rölantideyse rölantinin başladığı anı döner, yoksa null.
    // Bu rölantide zaten alarm verildiyse de null: rölanti başına tek alarm.
    // Süre konum kayıtlarının saatiyle (cihazın ölçüm anı) hesaplanır, sunucunun saatiyle değil.
    private async Task<DateTime?> GetIdleSinceIfLongAsync(LocationRecord location, CancellationToken cancellationToken)
    {
        // Rölanti en son ne zaman kesildi? (araç hareket etti ya da kontak kapandı)
        var lastNotIdleAt = await db.LocationRecords
            .Where(l => l.VehicleId == location.VehicleId
                        && (l.SpeedKmh >= AlarmRules.StationarySpeedKmh || !l.IgnitionOn))
            .MaxAsync(l => (DateTime?)l.RecordedAt, cancellationToken);

        // Rölantinin ilk kaydı: kesildiği andan sonraki ilk kayıt (hiç kesilmediyse aracın ilk kaydı).
        // Bu kayıt henüz kaydedilmediği için sorguda yok; rölantinin ilk kaydıysa sonuç null olur (süre 0).
        var idleSince = await db.LocationRecords
            .Where(l => l.VehicleId == location.VehicleId && (lastNotIdleAt == null || l.RecordedAt > lastNotIdleAt))
            .MinAsync(l => (DateTime?)l.RecordedAt, cancellationToken);
        if (idleSince is not DateTime since || !AlarmRules.IsLongIdle(since, location.RecordedAt))
            return null;

        var alreadyAlarmed = await db.Alarms.AnyAsync(
            a => a.VehicleId == location.VehicleId
                 && a.AlarmType == AlarmTypes.Idle
                 && (lastNotIdleAt == null || a.Location!.RecordedAt > lastNotIdleAt),
            cancellationToken);
        return alreadyAlarmed ? null : since;
    }

    // Araç durduğundan beri yakıt eşikten fazla düştüyse durduğu andaki yakıtı döner, yoksa null.
    // Bu duruşta zaten hırsızlık alarmı verildiyse de null: yakıt azalmaya devam etse de duruş başına tek alarm.
    private async Task<decimal?> GetFuelWhenStoppedIfDrainedAsync(
        int vehicleId, decimal currentFuelPercent, CancellationToken cancellationToken)
    {
        // Araç en son ne zaman hareket ediyordu? Duruş o an başladı (hiç hareket etmediyse ilk kaydından beri duruyor).
        var lastMovingAt = await db.LocationRecords
            .Where(l => l.VehicleId == vehicleId && l.SpeedKmh >= AlarmRules.StationarySpeedKmh)
            .MaxAsync(l => (DateTime?)l.RecordedAt, cancellationToken);

        // Durduğundan beri ölçülen en yüksek yakıt (son hareketli kayıt dahil: durduğu andaki yakıt).
        // En yükseğini alıyoruz: araç dururken depo doldurulup sonra boşaltılırsa da yakalansın.
        var fuelWhenStopped = await db.LocationRecords
            .Where(l => l.VehicleId == vehicleId && (lastMovingAt == null || l.RecordedAt >= lastMovingAt))
            .MaxAsync(l => l.FuelPercent, cancellationToken);
        if (fuelWhenStopped is not decimal fuel || !AlarmRules.IsFuelDrain(fuel, currentFuelPercent))
            return null;

        // Saatler aynı kaynaktan karşılaştırılsın diye alarmın kendi saatine değil, bağlı olduğu konumun saatine bakılır
        var alreadyAlarmed = await db.Alarms.AnyAsync(
            a => a.VehicleId == vehicleId
                 && a.AlarmType == AlarmTypes.FuelDrain
                 && (lastMovingAt == null || a.Location!.RecordedAt >= lastMovingAt),
            cancellationToken);
        return alreadyAlarmed ? null : fuel;
    }

    // Bu aracın bu türdeki son alarmı ne zamandı? (bekleme süresi kontrolü için; hiç yoksa null)
    private Task<DateTime?> GetLastAlarmAtAsync(int vehicleId, string alarmType, CancellationToken cancellationToken) =>
        db.Alarms
            .Where(a => a.VehicleId == vehicleId && a.AlarmType == alarmType)
            .MaxAsync(a => (DateTime?)a.CreatedAt, cancellationToken);

    private Alarm AddAlarm(LocationRecord location, string alarmType, string message)
    {
        var alarm = new Alarm
        {
            VehicleId = location.VehicleId,
            Location = location,        // LocationId'yi EF Core, konum kaydedildikten sonra kendisi doldurur
            AlarmType = alarmType,
            Message = message
        };
        db.Alarms.Add(alarm);
        return alarm;
    }

    // En yeni alarmlar (en fazla 200)
    public Task<List<AlarmDto>> GetAllAsync(bool unreadOnly, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, MaxTake);   // biri ?take=1000000 yazıp sunucuyu yormasın

        var query = db.Alarms.AsQueryable();
        if (unreadOnly)
            query = query.Where(a => !a.IsRead);

        return query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AlarmId)
            .Take(take)
            .Select(ToDto)
            .ToListAsync(cancellationToken);
    }

    // Alarm yoksa null döner
    public Task<AlarmDto?> GetByIdAsync(long alarmId, CancellationToken cancellationToken) =>
        db.Alarms
            .Where(a => a.AlarmId == alarmId)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken) =>
        db.Alarms.CountAsync(a => !a.IsRead, cancellationToken);

    // Alarm bulunamazsa false döner
    public async Task<bool> MarkAsReadAsync(long alarmId, CancellationToken cancellationToken)
    {
        var alarm = await db.Alarms.FindAsync([alarmId], cancellationToken);
        if (alarm is null)
            return false;

        alarm.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);   // EF Core sadece değişen sütunu günceller: UPDATE ... SET IsRead = 1
        logger.LogInformation("Alarm #{AlarmId} okundu olarak işaretlendi", alarmId);
        await live.Clients.All.AlarmsChanged();   // diğer açık sekmelerdeki rozet de güncellensin
        return true;
    }

    public async Task MarkAllAsReadAsync(CancellationToken cancellationToken)
    {
        // Kayıtları tek tek belleğe almadan, tek bir UPDATE sorgusuyla. Kaç satırın değiştiğini döner.
        var count = await db.Alarms
            .Where(a => !a.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true), cancellationToken);
        logger.LogInformation("{Count} alarm okundu olarak işaretlendi", count);
        if (count > 0)
            await live.Clients.All.AlarmsChanged();
    }
}
