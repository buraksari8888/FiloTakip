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

    // GET /api/vehicles/5 -> tek araç, yoksa 404
    [HttpGet("{id:int}")]
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
