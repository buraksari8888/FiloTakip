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

    // Yeni gelen konum için gereken alarmları ekler (hız, düşük yakıt). KAYDETMEZ: kaydetme işini çağıran yapar,
    // böylece konum ve alarmlar tek SaveChanges ile birlikte kaydedilir (ya hepsi ya hiçbiri).
    // previous: aracın bir önceki konumu (ilk konumuysa null). Eklenen alarmları döner, çoğu zaman boş liste.
    public async Task<List<Alarm>> AddAlarmsIfNeededAsync(
        LocationRecord location,
        short speedLimitKmh,
        LocationRecord? previous,
        CancellationToken cancellationToken)
    {
        var added = new List<Alarm>();
        var now = DateTime.UtcNow;

        // İlk kontrol veritabanına gitmeden yapılır: araç hızlı değilse son alarmı sorgulamaya gerek yok
        // (konumların çoğu bu durumda)
        if (AlarmRules.IsSpeeding(speedLimitKmh, location.SpeedKmh)
            && AlarmRules.ShouldCreateSpeedAlarm(
                speedLimitKmh, previous?.SpeedKmh, location.SpeedKmh,
                await GetLastAlarmAtAsync(location.VehicleId, AlarmTypes.SpeedLimit, cancellationToken), now))
        {
            added.Add(AddAlarm(location, AlarmTypes.SpeedLimit,
                AlarmRules.SpeedViolationMessage(speedLimitKmh, location.SpeedKmh)));
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

        return added;
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
