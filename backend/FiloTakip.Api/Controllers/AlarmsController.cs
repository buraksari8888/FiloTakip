using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Controllers;

[ApiController]
[Route("api/alarms")]
public class AlarmsController(FiloTakipDbContext db) : ControllerBase
{
    // GET /api/alarms?unreadOnly=true&take=50 -> en yeni alarmlar
    [HttpGet]
    public async Task<List<AlarmDto>> GetAll(bool unreadOnly = false, int take = 50)
    {
        take = Math.Clamp(take, 1, 200);   // biri ?take=1000000 yazıp sunucuyu yormasın

        var query = db.Alarms.AsQueryable();
        if (unreadOnly)
            query = query.Where(a => !a.IsRead);

        return await query
            .OrderByDescending(a => a.CreatedAt)
            .ThenByDescending(a => a.AlarmId)
            .Take(take)
            .Select(a => new AlarmDto(
                a.AlarmId,
                a.VehicleId,
                a.Vehicle!.Plate,
                a.AlarmType,
                a.Message,
                a.IsRead,
                a.CreatedAt,
                a.Location == null ? null : a.Location.Latitude,
                a.Location == null ? null : a.Location.Longitude))
            .ToListAsync();
    }

    // GET /api/alarms/unread-count -> okunmamış alarm sayısı (üst menüdeki rozet için)
    [HttpGet("unread-count")]
    public async Task<int> GetUnreadCount()
    {
        return await db.Alarms.CountAsync(a => !a.IsRead);
    }

    // POST /api/alarms/5/read -> alarmı okundu işaretle
    [HttpPost("{id:long}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkAsRead(long id)
    {
        var alarm = await db.Alarms.FindAsync(id);
        if (alarm is null)
            return NotFound();

        alarm.IsRead = true;
        await db.SaveChangesAsync();   // EF Core sadece değişen sütunu günceller: UPDATE ... SET IsRead = 1
        return NoContent();
    }

    // POST /api/alarms/read-all -> bütün alarmları okundu işaretle
    [HttpPost("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllAsRead()
    {
        // Kayıtları tek tek belleğe almadan, tek bir UPDATE sorgusuyla
        await db.Alarms
            .Where(a => !a.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true));
        return NoContent();
    }
}
