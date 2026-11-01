namespace FiloTakip.Api.Services;

// Harita hesapları
public static class GeoMath
{
    private const double EarthRadiusMeters = 6_371_000;

    // İki koordinat arasındaki kuş uçuşu mesafe, metre (Haversine formülü, Dünya'yı küre kabul eder).
    // Simülatördeki distance_km ve haritadaki izin distanceKm'si ile aynı formül.
    public static double DistanceMeters(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        var phi1 = ToRadians(lat1);
        var phi2 = ToRadians(lat2);
        var dPhi = ToRadians(lat2 - lat1);
        var dLambda = ToRadians(lon2 - lon1);
        var a = Math.Pow(Math.Sin(dPhi / 2), 2)
                + Math.Cos(phi1) * Math.Cos(phi2) * Math.Pow(Math.Sin(dLambda / 2), 2);
        return 2 * EarthRadiusMeters * Math.Asin(Math.Sqrt(a));
    }

    // Bir enlem derecesi kaç metre (boylam derecesi ise kutuplara doğru kısalır: bunun cos(enlem) katı)
    private const double MetersPerDegree = EarthRadiusMeters * Math.PI / 180;

    // Noktanın, iki nokta arasındaki düz parçaya en kısa uzaklığı, metre.
    // Birkaç yüz metrelik mesafelerde Dünya'yı düz kabul etmek yeterli: derece farkları metreye çevrilip
    // düzlemde (x: doğu, y: kuzey) hesaplanır. Nokta orijinde, parçanın uçları ona göre A ve B.
    public static double DistanceToSegmentMeters(
        decimal latitude, decimal longitude,
        decimal startLatitude, decimal startLongitude, decimal endLatitude, decimal endLongitude)
    {
        var metersPerDegreeLon = MetersPerDegree * Math.Cos(ToRadians(latitude));
        var ax = (double)(startLongitude - longitude) * metersPerDegreeLon;
        var ay = (double)(startLatitude - latitude) * MetersPerDegree;
        var dx = (double)(endLongitude - startLongitude) * metersPerDegreeLon;   // A'dan B'ye
        var dy = (double)(endLatitude - startLatitude) * MetersPerDegree;

        // Parçanın noktaya en yakın yeri A + t * (B - A); t = 0 A'nın, t = 1 B'nin kendisi.
        // Dik izdüşüm parçanın dışına düşüyorsa en yakın yer o taraftaki uçtur.
        var lengthSquared = dx * dx + dy * dy;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(-(ax * dx + ay * dy) / lengthSquared, 0, 1);
        return Math.Sqrt(Math.Pow(ax + t * dx, 2) + Math.Pow(ay + t * dy, 2));
    }

    private static double ToRadians(decimal degrees) => (double)degrees * Math.PI / 180;
}
