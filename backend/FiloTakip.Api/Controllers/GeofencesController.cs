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
}
