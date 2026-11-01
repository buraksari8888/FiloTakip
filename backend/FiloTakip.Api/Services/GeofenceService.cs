using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Bölgelerle ilgili okuma işlemleri
public class GeofenceService(FiloTakipDbContext db)
{
    // Aktif bölgeler, ada göre sıralı
    public Task<List<GeofenceDto>> GetActiveAsync(CancellationToken cancellationToken) =>
        db.Geofences
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .Select(g => new GeofenceDto(
                g.GeofenceId,
                g.Name,
                g.Latitude,
                g.Longitude,
                g.RadiusMeters,
                g.AlarmOnEnter,
                g.AlarmOnExit))
            .ToListAsync(cancellationToken);
}
