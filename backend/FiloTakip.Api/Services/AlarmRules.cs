using System.Globalization;

namespace FiloTakip.Api.Services;

// Hangi durumda alarm üretileceğine karar veren kurallar.
// Veritabanına dokunmaz, sadece hesap yapar; bu yüzden okuması ve test etmesi kolaydır.
public static class AlarmRules
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // Hız alarmı sadece ihlalin BAŞLADIĞI anda üretilir: önceki konumda sınırın altındaydı, şimdi üstünde.
    // Böylece araç 5 dakika boyunca hızlı giderse 100 değil, 1 alarm oluşur.
    public static bool IsNewSpeedViolation(short speedLimitKmh, decimal? previousSpeedKmh, decimal currentSpeedKmh)
    {
        var isSpeeding = currentSpeedKmh > speedLimitKmh;
        var wasSpeeding = previousSpeedKmh is not null && previousSpeedKmh > speedLimitKmh;
        return isSpeeding && !wasSpeeding;
    }

    public static string SpeedViolationMessage(short speedLimitKmh, decimal speedKmh) =>
        $"Hız sınırı aşıldı: {speedKmh.ToString("0.#", Turkish)} km/s (sınır {speedLimitKmh})";
}
