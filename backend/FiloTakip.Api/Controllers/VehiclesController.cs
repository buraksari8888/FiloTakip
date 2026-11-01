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

    // Geçmiş sorgusunda izin verilen en uzun aralık. 3 saniyede bir konumla 1 gün ~29 bin kayıt (~4 MB JSON);
    // daha uzunu hem yavaş olur hem de tarayıcıda oynatmak için gereksiz.
    private static readonly TimeSpan MaxHistoryRange = TimeSpan.FromDays(1);

    // GET /api/vehicles/5/history?from=2026-10-31T06:00:00Z&to=2026-10-31T09:00:00Z
    // -> aracın bu aralıktaki konumları, eskiden yeniye (geçmiş rota oynatma). Aralık en fazla 1 gün.
    // DateTimeOffset: adresteki saat dilimi ("Z" ya da "+03:00") korunur, UTC'ye doğru çevrilir.
    [HttpGet("{id:int}/history")]
    [ProducesResponseType<List<LocationDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<List<LocationDto>>> GetHistory(
        int id, [Required] DateTimeOffset? from, [Required] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        if (to <= from)
            ModelState.AddModelError(nameof(to), "Bitiş zamanı başlangıçtan sonra olmalı.");
        else if (to - from > MaxHistoryRange)
            ModelState.AddModelError(nameof(to), "Aralık en fazla 24 saat olabilir.");
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var history = await locations.GetBetweenAsync(id, from!.Value.UtcDateTime, to!.Value.UtcDateTime, cancellationToken);
        if (history is null)
            return NotFound();

        return history;
    }
}
