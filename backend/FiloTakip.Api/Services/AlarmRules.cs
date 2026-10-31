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

    // Yakıt bu yüzdenin altına düşünce alarm (frontend'deki LOW_FUEL_PERCENT ile aynı)
    public const decimal LowFuelPercent = 15;

    // Aynı araca bu süre içinde ikinci düşük yakıt alarmı verilmez. Yakıt sensörü dalgalanır
    // (depo çalkalanınca %14,9 -> %15,2 -> %14,8 gibi gidip gelebilir), her seferinde alarm yağmasın.
    public static readonly TimeSpan LowFuelAlarmCooldown = TimeSpan.FromMinutes(10);

    public static bool IsLowFuel(decimal fuelPercent) => fuelPercent < LowFuelPercent;

    // Yeni bir düşük yakıt alarmı üretilmeli mi? Hız alarmıyla aynı mantık: sadece eşiğin altına
    // indiği an sayılır, yakıt düşük kaldıkça (depo dolana kadar) her konumda yeni alarm üretilmez.
    public static bool ShouldCreateLowFuelAlarm(
        decimal? previousFuelPercent,
        decimal currentFuelPercent,
        DateTime? lastLowFuelAlarmAt,
        DateTime now)
    {
        if (!IsLowFuel(currentFuelPercent))
            return false;

        if (previousFuelPercent is decimal previous && IsLowFuel(previous))
            return false;

        if (lastLowFuelAlarmAt is DateTime last && now - last < LowFuelAlarmCooldown)
            return false;

        return true;
    }

    public static string LowFuelMessage(decimal fuelPercent) =>
        $"Yakıt seviyesi düşük: %{fuelPercent.ToString("0.#", Turkish)}";

    // Araç bu hızın altındaysa duruyor sayılır (GPS, duran araçta bile 1-2 km/s gösterebilir)
    public const decimal StationarySpeedKmh = 3;

    // Araç dururken yakıt bu kadar puan düşerse hırsızlık şüphesi. Duran araçta yakıt neredeyse hiç
    // harcanmaz; sensör dalgalanması da ±1-2 puandır, 5 puanlık düşüş bunlarla açıklanamaz.
    public const decimal FuelDrainPercent = 5;

    public static bool IsStationary(decimal speedKmh) => speedKmh < StationarySpeedKmh;

    // Araç durduğundan beri yakıt eşik kadar düştü mü? fuelWhenStopped: durduğundan beri ölçülen en yüksek yakıt
    public static bool IsFuelDrain(decimal fuelWhenStopped, decimal currentFuelPercent) =>
        fuelWhenStopped - currentFuelPercent >= FuelDrainPercent;

    public static string FuelDrainMessage(decimal fuelWhenStopped, decimal currentFuelPercent) =>
        $"Araç dururken yakıt düştü: %{fuelWhenStopped.ToString("0.#", Turkish)} → " +
        $"%{currentFuelPercent.ToString("0.#", Turkish)} (hırsızlık şüphesi)";

    // Rölanti: motor çalışıyor ama araç gitmiyor. Kırmızı ışıkta ya da kısa beklemede normal,
    // bu süreden uzun sürerse boşa yakıt yakılıyor demektir.
    public static readonly TimeSpan IdleAlarmAfter = TimeSpan.FromMinutes(5);

    public static bool IsIdling(bool ignitionOn, decimal speedKmh) => ignitionOn && IsStationary(speedKmh);

    // idleSince: rölantinin başladığı an (o rölantinin ilk konum kaydı), now: son konum kaydının saati
    public static bool IsLongIdle(DateTime idleSince, DateTime now) => now - idleSince >= IdleAlarmAfter;

    public static string IdleMessage(TimeSpan idleFor) =>
        $"Uzun rölanti: motor {(int)idleFor.TotalMinutes} dakikadır çalışıyor, araç yerinde bekliyor";
}
