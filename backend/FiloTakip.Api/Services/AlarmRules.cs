using System.Globalization;

namespace FiloTakip.Api.Services;

// Hangi durumda alarm üretileceğine karar veren kurallar.
// Veritabanına dokunmaz, sadece hesap yapar; bu yüzden okuması ve test etmesi kolaydır.
public static class AlarmRules
{
    // Sınırı bu kadar aşmadıkça alarm yok (90'lık yolda 95'e kadar göz yumulur)
    public const decimal SpeedToleranceKmh = 5;

    // Aynı araca bu süre içinde ikinci hız alarmı verilmez
    // (araç sınırın etrafında gidip geldiğinde birkaç saniyede bir alarm yağmasın)
    public static readonly TimeSpan SpeedAlarmCooldown = TimeSpan.FromMinutes(5);

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    // Hız, toleransla birlikte sınırın üstünde mi?
    public static bool IsSpeeding(short speedLimitKmh, decimal speedKmh) =>
        speedKmh > speedLimitKmh + SpeedToleranceKmh;

    // Yeni bir hız alarmı üretilmeli mi?
    public static bool ShouldCreateSpeedAlarm(
        short speedLimitKmh,
        decimal? previousSpeedKmh,
        decimal currentSpeedKmh,
        DateTime? lastSpeedAlarmAt,
        DateTime now)
    {
        if (!IsSpeeding(speedLimitKmh, currentSpeedKmh))
            return false;

        // İhlal zaten sürüyorsa (önceki konumda da hızlıydı) yeni alarm yok:
        // araç 5 dakika boyunca hızlı giderse 100 değil, 1 alarm oluşsun
        if (previousSpeedKmh is decimal previous && IsSpeeding(speedLimitKmh, previous))
            return false;

        // Son 5 dakikada bu araca zaten hız alarmı verildiyse yeni alarm yok
        if (lastSpeedAlarmAt is DateTime last && now - last < SpeedAlarmCooldown)
            return false;

        return true;
    }

    public static string SpeedViolationMessage(short speedLimitKmh, decimal speedKmh) =>
        $"Hız sınırı aşıldı: {speedKmh.ToString("0.#", Turkish)} km/s (sınır {speedLimitKmh})";
}
