using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Alarmlarla ilgili her şey: yeni alarm üretme, listeleme, okundu işaretleme
public class AlarmService(FiloTakipDbContext db)
{
    private const int MaxTake = 200;

    // Yeni gelen konum için gerekiyorsa hız alarmı ekler. KAYDETMEZ: kaydetme işini çağıran yapar,
    // böylece konum ve alarm tek SaveChanges ile birlikte kaydedilir (ya ikisi birden ya hiçbiri).
    public async Task AddSpeedAlarmIfNeededAsync(
        LocationRecord location,
        short speedLimitKmh,
        decimal? previousSpeedKmh,
        CancellationToken cancellationToken)
    {
        // Araç hızlı değilse veritabanına hiç gitme (konumların çoğu bu durumda)
        if (!AlarmRules.IsSpeeding(speedLimitKmh, location.SpeedKmh))
            return;

        // Bu aracın son hız alarmı ne zamandı? (bekleme süresi kontrolü için)
        var lastSpeedAlarmAt = await db.Alarms
            .Where(a => a.VehicleId == location.VehicleId && a.AlarmType == AlarmTypes.SpeedLimit)
            .MaxAsync(a => (DateTime?)a.CreatedAt, cancellationToken);

        if (!AlarmRules.ShouldCreateSpeedAlarm(
                speedLimitKmh, previousSpeedKmh, location.SpeedKmh, lastSpeedAlarmAt, DateTime.UtcNow))
            return;

        db.Alarms.Add(new Alarm
        {
            VehicleId = location.VehicleId,
            Location = location,        // LocationId'yi EF Core, konum kaydedildikten sonra kendisi doldurur
            AlarmType = AlarmTypes.SpeedLimit,
            Message = AlarmRules.SpeedViolationMessage(speedLimitKmh, location.SpeedKmh)
        });
    }

    // En yeni alarmlar (en fazla 200)
    public Task<List<AlarmDto>> GetAllAsync(bool unreadOnly, int take, CancellationToken cancellationToken)
    {
        take = Math.Clamp(take, 1, MaxTake);   // biri ?take=1000000 yazıp sunucuyu yormasın

        var query = db.Alarms.AsQueryable();
        if (unreadOnly)
            query = query.Where(a => !a.IsRead);

        return query
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
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(CancellationToken cancellationToken) =>
        db.Alarms.CountAsync(a => !a.IsRead, cancellationToken);

    // Alarm bulunamazsa false döner
    public async Task<bool> MarkAsReadAsync(long alarmId, CancellationToken cancellationToken)
    {
        var alarm = await db.Alarms.FindAsync([alarmId], cancellationToken);
        if (alarm is null)
            return false;

        alarm.IsRead = true;
        await db.SaveChangesAsync(cancellationToken);   // EF Core sadece değişen sütunu günceller: UPDATE ... SET IsRead = 1
        return true;
    }

    public Task MarkAllAsReadAsync(CancellationToken cancellationToken) =>
        // Kayıtları tek tek belleğe almadan, tek bir UPDATE sorgusuyla
        db.Alarms
            .Where(a => !a.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.IsRead, true), cancellationToken);
}
