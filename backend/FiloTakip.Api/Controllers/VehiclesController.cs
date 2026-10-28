using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController(FiloTakipDbContext db) : ControllerBase   // db'yi .NET otomatik verir (Dependency Injection)
{
    // GET /api/vehicles -> tüm araçlar
    [HttpGet]
    public async Task<List<VehicleDto>> GetAll()
    {
        return await db.Vehicles
            .OrderBy(v => v.Plate)
            .Select(v => new VehicleDto(
                v.VehicleId,
                v.Plate,
                v.Brand,
                v.Model,
                v.ModelYear,
                v.SpeedLimitKmh,
                v.IsActive,
                v.DriverId,
                v.Driver == null ? null : v.Driver.FirstName + " " + v.Driver.LastName))
            .ToListAsync();
    }

    // GET /api/vehicles/last-locations -> her aktif aracın son konumu (harita bunu kullanır)
    [HttpGet("last-locations")]
    public async Task<List<VehicleLastLocationDto>> GetLastLocations()
    {
        return await db.VehicleLastLocations
            .OrderBy(v => v.Plate)
            .Select(v => new VehicleLastLocationDto(
                v.VehicleId,
                v.Plate,
                v.Brand,
                v.Model,
                v.SpeedLimitKmh,
                v.DriverName,
                v.Latitude,
                v.Longitude,
                v.SpeedKmh,
                v.FuelPercent,
                v.IgnitionOn,
                v.RecordedAt))
            .ToListAsync();
    }

    // GET /api/vehicles/5 -> tek araç, yoksa 404
    [HttpGet("{id:int}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDto>> GetById(int id)
    {
        var vehicle = await db.Vehicles
            .Where(v => v.VehicleId == id)
            .Select(v => new VehicleDto(
                v.VehicleId,
                v.Plate,
                v.Brand,
                v.Model,
                v.ModelYear,
                v.SpeedLimitKmh,
                v.IsActive,
                v.DriverId,
                v.Driver == null ? null : v.Driver.FirstName + " " + v.Driver.LastName))
            .FirstOrDefaultAsync();

        if (vehicle is null)
            return NotFound();

        return vehicle;
    }
}
