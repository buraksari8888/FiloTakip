using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Models;
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
        var vehicleExists = await db.Vehicles.AnyAsync(v => v.VehicleId == dto.VehicleId);
        if (!vehicleExists)
        {
            ModelState.AddModelError(nameof(dto.VehicleId), $"{dto.VehicleId} numaralı araç bulunamadı.");
            return ValidationProblem(ModelState);
        }

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
        await db.SaveChangesAsync();      // INSERT burada çalışır, LocationId de dolar

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
