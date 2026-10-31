using System.ComponentModel.DataAnnotations;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public class VehiclesController(VehicleService vehicles, LocationService locations) : ControllerBase   // servisleri .NET otomatik verir (Dependency Injection)
{
    // GET /api/vehicles -> tüm araçlar
    // cancellationToken: kullanıcı isteği iptal ederse (sayfayı kapatırsa) .NET bunu otomatik doldurur
    [HttpGet]
    public Task<List<VehicleDto>> GetAll(CancellationToken cancellationToken) =>
        vehicles.GetAllAsync(cancellationToken);

    // GET /api/vehicles/last-locations -> her aktif aracın son konumu (harita bunu kullanır)
    [HttpGet("last-locations")]
    public Task<List<VehicleLastLocationDto>> GetLastLocations(CancellationToken cancellationToken) =>
        vehicles.GetLastLocationsAsync(cancellationToken);

    // GET /api/vehicles/5 -> tek araç, yoksa 404
    [HttpGet("{id:int}")]
    [ProducesResponseType<VehicleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VehicleDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var vehicle = await vehicles.GetByIdAsync(id, cancellationToken);
        if (vehicle is null)
            return NotFound();

        return vehicle;
    }

    // GET /api/vehicles/5/locations?minutes=30 -> aracın son 30 dakikadaki konumları, eskiden yeniye (haritadaki iz)
    // minutes 1-1440 (en fazla 1 gün) dışındaysa otomatik 400 döner
    [HttpGet("{id:int}/locations")]
    [ProducesResponseType<List<LocationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LocationDto>>> GetRecentLocations(
        int id, [Range(1, 1440)] int minutes = 30, CancellationToken cancellationToken = default)
    {
        var recent = await locations.GetRecentAsync(id, minutes, cancellationToken);
        if (recent is null)
            return NotFound();

        return recent;
    }
}
