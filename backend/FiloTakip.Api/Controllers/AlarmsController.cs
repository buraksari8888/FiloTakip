using FiloTakip.Api.Dtos;
using FiloTakip.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/alarms")]
public class AlarmsController(AlarmService alarms) : ControllerBase
{
    // GET /api/alarms?unreadOnly=true&take=50 -> en yeni alarmlar
    [HttpGet]
    public Task<List<AlarmDto>> GetAll(bool unreadOnly = false, int take = 50, CancellationToken cancellationToken = default) =>
        alarms.GetAllAsync(unreadOnly, take, cancellationToken);

    // GET /api/alarms/unread-count -> okunmamış alarm sayısı (üst menüdeki rozet için)
    [HttpGet("unread-count")]
    public Task<int> GetUnreadCount(CancellationToken cancellationToken) =>
        alarms.GetUnreadCountAsync(cancellationToken);

    // POST /api/alarms/5/read -> alarmı okundu işaretle
    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(long id, CancellationToken cancellationToken)
    {
        var found = await alarms.MarkAsReadAsync(id, cancellationToken);
        return found ? NoContent() : NotFound();
    }

    // POST /api/alarms/read-all -> bütün alarmları okundu işaretle
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead(CancellationToken cancellationToken)
    {
        await alarms.MarkAllAsReadAsync(cancellationToken);
        return NoContent();
    }
}
