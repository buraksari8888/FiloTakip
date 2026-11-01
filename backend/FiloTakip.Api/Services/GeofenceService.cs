using System.Linq.Expressions;
using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Hubs;
using FiloTakip.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

public enum GeofenceChangeResult { Ok, NotFound, NameTaken }

// Bölgelerle ilgili işlemler: listeleme, ekleme, düzenleme, silme (pasife alma)
public class GeofenceService(
    FiloTakipDbContext db,
    IHubContext<LiveHub, ILiveClient> live,
    ILogger<GeofenceService> logger)
{
    // Aktif bölgelerde ad tekrar edemez (01_schema.sql / 10_geofence_names.sql)
    private const string NameIndex = "UX_Geofences_Name";

    // Geofence -> GeofenceDto dönüşümü tek yerde
    private static readonly Expression<Func<Geofence, GeofenceDto>> ToDto = g => new GeofenceDto(
        g.GeofenceId,
        g.Name,
        g.Latitude,
        g.Longitude,
        g.RadiusMeters,
        g.AlarmOnEnter,
        g.AlarmOnExit);

    // Aktif bölgeler, ada göre sıralı
    public Task<List<GeofenceDto>> GetActiveAsync(CancellationToken cancellationToken) =>
        db.Geofences
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    // Bölge yoksa (ya da silinmişse) null döner
    public Task<GeofenceDto?> GetByIdAsync(int geofenceId, CancellationToken cancellationToken) =>
        db.Geofences
            .Where(g => g.GeofenceId == geofenceId && g.IsActive)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    // Yeni bölge ekler. Aynı adda aktif bir bölge varsa eklemez, null döner.
    public async Task<GeofenceDto?> CreateAsync(SaveGeofenceDto dto, CancellationToken cancellationToken)
    {
        var name = VehicleRules.CleanText(dto.Name);
        if (await NameTakenAsync(name, null, cancellationToken))
            return null;

        var geofence = new Geofence();
        Apply(geofence, dto, name);
        db.Geofences.Add(geofence);
        if (!await TrySaveAsync(cancellationToken))
            return null;

        logger.LogInformation("Bölge #{GeofenceId} eklendi: {Name}", geofence.GeofenceId, geofence.Name);
        await live.Clients.All.GeofencesChanged();
        return new GeofenceDto(geofence.GeofenceId, geofence.Name, geofence.Latitude, geofence.Longitude,
            geofence.RadiusMeters, geofence.AlarmOnEnter, geofence.AlarmOnExit);
    }

    // Bölgenin adını, yerini, büyüklüğünü ya da alarm ayarını değiştirir
    public async Task<GeofenceChangeResult> UpdateAsync(int geofenceId, SaveGeofenceDto dto, CancellationToken cancellationToken)
    {
        var geofence = await db.Geofences.FirstOrDefaultAsync(g => g.GeofenceId == geofenceId && g.IsActive, cancellationToken);
        if (geofence is null)
            return GeofenceChangeResult.NotFound;

        var name = VehicleRules.CleanText(dto.Name);
        if (await NameTakenAsync(name, geofenceId, cancellationToken))
            return GeofenceChangeResult.NameTaken;

        Apply(geofence, dto, name);
        if (!await TrySaveAsync(cancellationToken))
            return GeofenceChangeResult.NameTaken;

        logger.LogInformation("Bölge #{GeofenceId} güncellendi", geofenceId);
        await live.Clients.All.GeofencesChanged();
        return GeofenceChangeResult.Ok;
    }

    // Bölgeyi pasife alır: artık alarm üretmez ve haritada çizilmez; daha önce üretilen alarmlar silinmez.
    // Bölge yoksa false döner.
    public async Task<bool> DeactivateAsync(int geofenceId, CancellationToken cancellationToken)
    {
        var changed = await db.Geofences
            .Where(g => g.GeofenceId == geofenceId && g.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.IsActive, false), cancellationToken);
        if (changed == 0)
            return false;

        logger.LogInformation("Bölge #{GeofenceId} silindi (pasife alındı)", geofenceId);
        await live.Clients.All.GeofencesChanged();
        return true;
    }

    // Aynı adda başka bir aktif bölge var mı? (Sunucunun dili büyük/küçük harfe bakmıyor: "depo" ile "Depo" aynı)
    private Task<bool> NameTakenAsync(string name, int? exceptId, CancellationToken cancellationToken) =>
        db.Geofences.AnyAsync(g => g.IsActive && g.Name == name && g.GeofenceId != exceptId, cancellationToken);

    // Kaydeder; aynı ad az önce başka bir istekle alındıysa (iki kullanıcı aynı anda) false döner
    private async Task<bool> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, NameIndex))
        {
            return false;
        }
    }

    // Koordinatlar veritabanında 6 ondalık (~10 cm); fazlası yuvarlanır
    private static void Apply(Geofence geofence, SaveGeofenceDto dto, string name)
    {
        geofence.Name = name;
        geofence.Latitude = Math.Round(dto.Latitude, 6);
        geofence.Longitude = Math.Round(dto.Longitude, 6);
        geofence.RadiusMeters = dto.RadiusMeters;
        geofence.AlarmOnEnter = dto.AlarmOnEnter;
        geofence.AlarmOnExit = dto.AlarmOnExit;
    }
}
