using FiloTakip.Api.Dtos;
using FiloTakip.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/geofences")]
public class GeofencesController(GeofenceService geofences) : ControllerBase
{
    // GET /api/geofences -> aktif bölgeler (harita bunları daire olarak çizer)
    [HttpGet]
    public Task<List<GeofenceDto>> GetAll(CancellationToken cancellationToken) =>
        geofences.GetActiveAsync(cancellationToken);

    // GET /api/geofences/5 -> tek bölge, yoksa 404
    [HttpGet("{id:int}")]
    [ProducesResponseType<GeofenceDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GeofenceDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var geofence = await geofences.GetByIdAsync(id, cancellationToken);
        if (geofence is null)
            return NotFound();

        return geofence;
    }

    // POST /api/geofences -> yeni bölge. Aynı adda bölge varsa 409.
    [HttpPost]
    [ProducesResponseType<GeofenceDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GeofenceDto>> Create(SaveGeofenceDto dto, CancellationToken cancellationToken)
    {
        var geofence = await geofences.CreateAsync(dto, cancellationToken);
        if (geofence is null)
            return NameTaken(dto);

        return CreatedAtAction(nameof(GetById), new { id = geofence.GeofenceId }, geofence);
    }

    // PUT /api/geofences/5 -> bölgenin adı, yeri, yarıçapı ya da alarm ayarı değişir
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, SaveGeofenceDto dto, CancellationToken cancellationToken) =>
        await geofences.UpdateAsync(id, dto, cancellationToken) switch
        {
            GeofenceChangeResult.NotFound => NotFound(),
            GeofenceChangeResult.NameTaken => NameTaken(dto),
            _ => NoContent()
        };

    // DELETE /api/geofences/5 -> bölgeyi pasife alır: alarm üretmez, haritadan kalkar; eski alarmlar silinmez
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Deactivate(int id, CancellationToken cancellationToken) =>
        await geofences.DeactivateAsync(id, cancellationToken) ? NoContent() : NotFound();

    private ObjectResult NameTaken(SaveGeofenceDto dto) => Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: $"\"{VehicleRules.CleanText(dto.Name)}\" adında bir bölge zaten var.");
}
