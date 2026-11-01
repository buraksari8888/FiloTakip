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

    private static double ToRadians(decimal degrees) => (double)degrees * Math.PI / 180;
}
