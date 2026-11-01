using FiloTakip.Api.Dtos;
using FiloTakip.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/drivers")]
public class DriversController(DriverService drivers) : ControllerBase
{
    // GET /api/drivers -> aktif sürücüler (her birinin şu anki aracıyla)
    [HttpGet]
    public Task<List<DriverDto>> GetAll(CancellationToken cancellationToken) =>
        drivers.GetAllAsync(cancellationToken);

    // GET /api/drivers/5 -> tek sürücü, yoksa 404
    [HttpGet("{id:int}")]
    [ProducesResponseType<DriverDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DriverDto>> GetById(int id, CancellationToken cancellationToken)
    {
        var driver = await drivers.GetByIdAsync(id, cancellationToken);
        if (driver is null)
            return NotFound();

        return driver;
    }

    // POST /api/drivers -> yeni sürücü. Ehliyet numarası başka bir sürücüdeyse 409.
    [HttpPost]
    [ProducesResponseType<DriverDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DriverDto>> Create(CreateDriverDto dto, CancellationToken cancellationToken)
    {
        var driver = await drivers.CreateAsync(dto, cancellationToken);
        if (driver is null)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "Bu ehliyet numarası başka bir sürücüde kayıtlı.");

        return CreatedAtAction(nameof(GetById), new { id = driver.DriverId }, driver);
    }

    // PUT /api/drivers/5 { "firstName": ..., "lastName": ... } -> sürücünün adını düzeltir
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateName(int id, UpdateDriverNameDto dto, CancellationToken cancellationToken) =>
        await drivers.UpdateNameAsync(id, dto, cancellationToken) ? NoContent() : NotFound();
}
