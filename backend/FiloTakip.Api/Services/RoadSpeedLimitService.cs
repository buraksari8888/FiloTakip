using FiloTakip.Api.Data;
using FiloTakip.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace FiloTakip.Api.Services;

// Konumun hangi yolda olduğunu bulur ve o yolun hız sınırını döner.
// Gerçek sistemlerde de böyle: GPS cihazı yolun sınırını bilmez, sunucu harita verisinden bulur.
public class RoadSpeedLimitService(
    FiloTakipDbContext db,
    IMemoryCache cache,
    ILogger<RoadSpeedLimitService> logger)
{
    // Konum en yakın yol parçasına bundan uzaksa bilinen bir yolda değil sayılır
    // (GPS konumu 10-20 metre sapabilir; şeritli bir caddenin genişliği de 20-30 metre)
    public const double MaxDistanceMeters = 30;

    private const string CacheKey = "RoadSegments";

    // Yol parçaları nadiren değişir (07_road_segments.sql çalıştırılınca). Her konumda veritabanından
    // binlerce satır okumak yerine bellekte tutulur, 10 dakikada bir tazelenir.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);

    // Konumun bulunduğu yolun hız sınırı; bilinen bir yolda değilse null
    public async Task<short?> GetSpeedLimitAsync(decimal latitude, decimal longitude, CancellationToken cancellationToken)
    {
        var segments = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheDuration;
            var all = await db.RoadSegments.AsNoTracking().ToListAsync(cancellationToken);
            logger.LogInformation("{Count} yol parçası belleğe alındı", all.Count);
            return all;
        });
        return FindSpeedLimit(segments!, latitude, longitude);
    }

    // En yakın yol parçasının sınırı (MaxDistanceMeters içinde hiç parça yoksa null).
    // Veritabanına dokunmaz, test edilebilsin diye ayrı.
    public static short? FindSpeedLimit(IEnumerable<RoadSegment> segments, decimal latitude, decimal longitude)
    {
        RoadSegment? nearest = null;
        var nearestDistance = MaxDistanceMeters;
        foreach (var segment in segments)
        {
            var distance = GeoMath.DistanceToSegmentMeters(
                latitude, longitude,
                segment.StartLatitude, segment.StartLongitude, segment.EndLatitude, segment.EndLongitude);
            if (distance <= nearestDistance)
            {
                nearest = segment;
                nearestDistance = distance;
            }
        }
        return nearest?.SpeedLimitKmh;
    }
}
