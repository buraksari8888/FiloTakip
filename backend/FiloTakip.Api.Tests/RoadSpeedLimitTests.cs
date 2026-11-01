using FiloTakip.Api.Models;
using FiloTakip.Api.Services;

namespace FiloTakip.Api.Tests;

// Konumun hangi yolda olduğunu bulma (en yakın yol parçası) testleri
public class RoadSpeedLimitTests
{
    // Doğu-batı yönünde iki yol parçası (enlem 41.0): batıda 90'lık, doğuda 50'lik.
    // Bu enlemde boylamın 0.001 derecesi ~84 metre, enlemin 0.001 derecesi ~111 metre.
    private static readonly RoadSegment Highway = Segment(90, 41.0m, 29.000m, 41.0m, 29.010m);
    private static readonly RoadSegment Street = Segment(50, 41.0m, 29.010m, 41.0m, 29.020m);
    private static readonly RoadSegment[] Segments = [Highway, Street];

    private static RoadSegment Segment(short limit, decimal lat1, decimal lon1, decimal lat2, decimal lon2) => new()
    {
        HighwayType = "test",
        SpeedLimitKmh = limit,
        StartLatitude = lat1,
        StartLongitude = lon1,
        EndLatitude = lat2,
        EndLongitude = lon2
    };

    [Theory]
    [InlineData(41.0, 29.005, 0)]         // tam parçanın üstünde
    [InlineData(41.0001, 29.005, 11.1)]   // parçanın 11 m kuzeyinde (dik uzaklık)
    [InlineData(41.0, 28.999, 84)]        // parçanın başlangıcının 84 m batısında (en yakın yer uç nokta)
    public void DistanceToSegmentMeters_MeasuresToNearestPointOfSegment(double lat, double lon, double expected)
    {
        var distance = GeoMath.DistanceToSegmentMeters(
            (decimal)lat, (decimal)lon,
            Highway.StartLatitude, Highway.StartLongitude, Highway.EndLatitude, Highway.EndLongitude);

        Assert.Equal(expected, distance, tolerance: 1);
    }

    [Theory]
    [InlineData(41.0, 29.005, 90)]       // batıdaki yolda
    [InlineData(41.0001, 29.015, 50)]    // doğudaki yolun 11 m yanında (GPS sapması)
    [InlineData(41.001, 29.015, null)]   // yoldan 111 m uzakta: bilinen bir yolda değil
    public void FindSpeedLimit_UsesNearestSegmentWithinMaxDistance(double lat, double lon, int? expected)
    {
        var limit = RoadSpeedLimitService.FindSpeedLimit(Segments, (decimal)lat, (decimal)lon);

        Assert.Equal((short?)expected, limit);
    }
}
