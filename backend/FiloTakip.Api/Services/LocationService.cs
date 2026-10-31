using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Hubs;
using FiloTakip.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Araçlardan gelen konumları kaydeder ve okur
public class LocationService(
    FiloTakipDbContext db,
    AlarmService alarms,
    IHubContext<LiveHub, ILiveClient> live,
    ILogger<LocationService> logger)
{
    // Konumu kaydeder, gerekiyorsa alarm üretir. Araç bulunamazsa null döner.
    public async Task<LocationDto?> CreateAsync(CreateLocationDto dto, CancellationToken cancellationToken)
    {
        // Olmayan bir araca konum yazılmasın (veritabanındaki FOREIGN KEY de engeller,
        // ama burada kontrol edersek kullanıcıya anlaşılır bir hata verebiliriz)
        var vehicle = await db.Vehicles
            .Where(v => v.VehicleId == dto.VehicleId)
            .Select(v => new { v.Plate, v.SpeedLimitKmh })
            .FirstOrDefaultAsync(cancellationToken);
        if (vehicle is null)
        {
            // Uyarı: genelde cihaz yanlış ayarlanmış ya da araç sistemden silinmiş demektir
            logger.LogWarning("Konum reddedildi: {VehicleId} numaralı araç yok", dto.VehicleId);
            return null;
        }

        // Aracın bir önceki konumu: yeni bir ihlal mi başladı (hız sınırı aşıldı, yakıt eşiğin altına indi),
        // yoksa zaten süren bir durum mu? (IX_LocationRecords_Vehicle_Time index'i tam bu sorgu için var,
        // milyonlarca kayıtta da hızlı çalışır). AsNoTracking: sadece okuyoruz, EF Core değişiklik takibi yapmasın.
        var previous = await db.LocationRecords
            .AsNoTracking()
            .Where(l => l.VehicleId == dto.VehicleId)
            .OrderByDescending(l => l.RecordedAt)
            .ThenByDescending(l => l.LocationId)   // RecordedAt saniye hassasiyetinde; aynı saniyede gelenlerde son ekleneni al
            .FirstOrDefaultAsync(cancellationToken);

        var record = new LocationRecord
        {
            VehicleId = dto.VehicleId,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            SpeedKmh = dto.SpeedKmh,
            FuelPercent = dto.FuelPercent,
            IgnitionOn = dto.IgnitionOn,
            RecordedAt = dto.RecordedAt?.ToUniversalTime() ?? DateTime.UtcNow
        };

        db.LocationRecords.Add(record);   // henüz veritabanına gitmedi, sadece "eklenecek" diye işaretlendi
        var newAlarms = await alarms.AddAlarmsIfNeededAsync(record, vehicle.SpeedLimitKmh, previous, cancellationToken);

        // Konum ve (varsa) alarmlar tek seferde kaydedilir: ya hepsi ya hiçbiri
        await db.SaveChangesAsync(cancellationToken);

        // Loglar kaydettikten SONRA: kaydetme hata verirse "alarm üretildi" diye yanlış bilgi yazmasınlar
        foreach (var alarm in newAlarms)
            logger.LogInformation("Alarm #{AlarmId} ({AlarmType}): {Plate} {Message}",
                alarm.AlarmId, alarm.AlarmType, vehicle.Plate, alarm.Message);

        // Her konumda yazılır, bu yüzden Debug: normalde gizli, gerekince appsettings'ten açılır
        logger.LogDebug("Konum #{LocationId} kaydedildi: {Plate} {SpeedKmh} km/s",
            record.LocationId, vehicle.Plate, record.SpeedKmh);

        // Açık ekranlara haber ver (kaydettikten sonra: kaydedilmemiş bir konumu göstermesinler)
        var location = ToDto(record);
        await live.Clients.All.LocationAdded(location);
        if (newAlarms.Count > 0)
            await live.Clients.All.AlarmsChanged();

        return location;
    }

    // Konum yoksa null döner
    public async Task<LocationDto?> GetByIdAsync(long locationId, CancellationToken cancellationToken)
    {
        var record = await db.LocationRecords.FindAsync([locationId], cancellationToken);
        return record is null ? null : ToDto(record);
    }

    // Aracın son X dakikadaki konumları, eskiden yeniye (haritada aracın arkasındaki iz için).
    // Araç yoksa null döner. IX_LocationRecords_Vehicle_Time index'i bu sorguyu da hızlandırır.
    public async Task<List<LocationDto>?> GetRecentAsync(int vehicleId, int minutes, CancellationToken cancellationToken)
    {
        if (!await db.Vehicles.AnyAsync(v => v.VehicleId == vehicleId, cancellationToken))
            return null;

        var since = DateTime.UtcNow.AddMinutes(-minutes);
        return await db.LocationRecords
            .Where(l => l.VehicleId == vehicleId && l.RecordedAt >= since)
            .OrderBy(l => l.RecordedAt)
            .ThenBy(l => l.LocationId)   // aynı saniyede gelenler eklenme sırasıyla
            .Select(l => ToDto(l))
            .ToListAsync(cancellationToken);
    }

    private static LocationDto ToDto(LocationRecord r) =>
        new(r.LocationId, r.VehicleId, r.Latitude, r.Longitude, r.SpeedKmh, r.FuelPercent, r.IgnitionOn, r.RecordedAt);
}
