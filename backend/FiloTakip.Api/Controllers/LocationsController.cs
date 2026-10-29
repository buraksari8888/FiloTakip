using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Models;
using FiloTakip.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController(FiloTakipDbContext db) : ControllerBase
{
    // POST /api/locations -> yeni konum kaydı ekler
    [HttpPost]
    [ProducesResponseType<LocationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocationDto>> Create(CreateLocationDto dto)
    {
        // Olmayan bir araca konum yazılmasın (veritabanındaki FOREIGN KEY de engeller,
        // ama burada kontrol edersek kullanıcıya anlaşılır bir hata verebiliriz)
        var speedLimitKmh = await db.Vehicles
            .Where(v => v.VehicleId == dto.VehicleId)
            .Select(v => (short?)v.SpeedLimitKmh)
            .FirstOrDefaultAsync();
        if (speedLimitKmh is null)
        {
            ModelState.AddModelError(nameof(dto.VehicleId), $"{dto.VehicleId} numaralı araç bulunamadı.");
            return ValidationProblem(ModelState);
        }

        // Aracın bir önceki hızı: yeni bir hız ihlali mi başladı, yoksa zaten süren bir ihlal mi?
        // (IX_LocationRecords_Vehicle_Time index'i tam bu sorgu için var, milyonlarca kayıtta da hızlı çalışır)
        var previousSpeedKmh = await db.LocationRecords
            .Where(l => l.VehicleId == dto.VehicleId)
            .OrderByDescending(l => l.RecordedAt)
            .ThenByDescending(l => l.LocationId)   // RecordedAt saniye hassasiyetinde; aynı saniyede gelenlerde son ekleneni al
            .Select(l => (decimal?)l.SpeedKmh)
            .FirstOrDefaultAsync();

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

        if (AlarmRules.IsSpeeding(speedLimitKmh.Value, record.SpeedKmh))
        {
            // Bu aracın son hız alarmı ne zamandı? (sadece araç hızlıyken soruyoruz,
            // her konumda gereksiz yere veritabanına gitmeyelim)
            var lastSpeedAlarmAt = await db.Alarms
                .Where(a => a.VehicleId == record.VehicleId && a.AlarmType == AlarmTypes.SpeedLimit)
                .MaxAsync(a => (DateTime?)a.CreatedAt);

            if (AlarmRules.ShouldCreateSpeedAlarm(
                    speedLimitKmh.Value, previousSpeedKmh, record.SpeedKmh, lastSpeedAlarmAt, DateTime.UtcNow))
            {
                db.Alarms.Add(new Alarm
                {
                    VehicleId = record.VehicleId,
                    Location = record,        // LocationId'yi EF Core, konum kaydedildikten sonra kendisi doldurur
                    AlarmType = AlarmTypes.SpeedLimit,
                    Message = AlarmRules.SpeedViolationMessage(speedLimitKmh.Value, record.SpeedKmh)
                });
            }
        }

        // Konum ve (varsa) alarm tek seferde kaydedilir: ya ikisi birden ya hiçbiri
        await db.SaveChangesAsync();

        // 201 Created + yeni kaydın adresi (Location başlığında /api/locations/{id})
        return CreatedAtAction(nameof(GetById), new { id = record.LocationId }, ToDto(record));
    }

    // GET /api/locations/5 -> tek konum kaydı
    [HttpGet("{id:long}")]
    [ProducesResponseType<LocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> GetById(long id)
    {
        var record = await db.LocationRecords.FindAsync(id);
        if (record is null)
            return NotFound();

        return ToDto(record);
    }

    private static LocationDto ToDto(LocationRecord r) =>
        new(r.LocationId, r.VehicleId, r.Latitude, r.Longitude, r.SpeedKmh, r.FuelPercent, r.IgnitionOn, r.RecordedAt);
}
