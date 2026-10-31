using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Araçlardan gelen konumları kaydeder ve okur
public class LocationService(FiloTakipDbContext db, AlarmService alarms, ILogger<LocationService> logger)
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

        // Aracın bir önceki hızı: yeni bir hız ihlali mi başladı, yoksa zaten süren bir ihlal mi?
        // (IX_LocationRecords_Vehicle_Time index'i tam bu sorgu için var, milyonlarca kayıtta da hızlı çalışır)
        var previousSpeedKmh = await db.LocationRecords
            .Where(l => l.VehicleId == dto.VehicleId)
            .OrderByDescending(l => l.RecordedAt)
            .ThenByDescending(l => l.LocationId)   // RecordedAt saniye hassasiyetinde; aynı saniyede gelenlerde son ekleneni al
            .Select(l => (decimal?)l.SpeedKmh)
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
        var alarm = await alarms.AddSpeedAlarmIfNeededAsync(record, vehicle.SpeedLimitKmh, previousSpeedKmh, cancellationToken);

        // Konum ve (varsa) alarm tek seferde kaydedilir: ya ikisi birden ya hiçbiri
        await db.SaveChangesAsync(cancellationToken);

        // Loglar kaydettikten SONRA: kaydetme hata verirse "alarm üretildi" diye yanlış bilgi yazmasınlar
        if (alarm is not null)
            logger.LogInformation("Hız alarmı #{AlarmId}: {Plate} {SpeedKmh} km/s (sınır {SpeedLimitKmh})",
                alarm.AlarmId, vehicle.Plate, record.SpeedKmh, vehicle.SpeedLimitKmh);

        // Her konumda yazılır, bu yüzden Debug: normalde gizli, gerekince appsettings'ten açılır
        logger.LogDebug("Konum #{LocationId} kaydedildi: {Plate} {SpeedKmh} km/s",
            record.LocationId, vehicle.Plate, record.SpeedKmh);

        return ToDto(record);
    }

    // Konum yoksa null döner
    public async Task<LocationDto?> GetByIdAsync(long locationId, CancellationToken cancellationToken)
    {
        var record = await db.LocationRecords.FindAsync([locationId], cancellationToken);
        return record is null ? null : ToDto(record);
    }

    private static LocationDto ToDto(LocationRecord r) =>
        new(r.LocationId, r.VehicleId, r.Latitude, r.Longitude, r.SpeedKmh, r.FuelPercent, r.IgnitionOn, r.RecordedAt);
}
