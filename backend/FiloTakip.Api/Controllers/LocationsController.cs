using FiloTakip.Api.Dtos;
using FiloTakip.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/locations")]
public class LocationsController(LocationService locations) : ControllerBase
{
    // POST /api/locations -> yeni konum kaydı ekler (gerekirse alarm da üretir)
    [HttpPost]
    [ProducesResponseType<LocationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<LocationDto>> Create(CreateLocationDto dto, CancellationToken cancellationToken)
    {
        var location = await locations.CreateAsync(dto, cancellationToken);
        if (location is null)
        {
            ModelState.AddModelError(nameof(dto.VehicleId), $"{dto.VehicleId} numaralı aktif araç bulunamadı.");
            return ValidationProblem(ModelState);
        }

        // 201 Created + yeni kaydın adresi (Location başlığında /api/locations/{id})
        return CreatedAtAction(nameof(GetById), new { id = location.LocationId }, location);
    }

    // GET /api/locations/5 -> tek konum kaydı
    [HttpGet("{id:long}")]
    [ProducesResponseType<LocationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LocationDto>> GetById(long id, CancellationToken cancellationToken)
    {
        var location = await locations.GetByIdAsync(id, cancellationToken);
        if (location is null)
            return NotFound();

        return location;
    }
}
