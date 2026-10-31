using FiloTakip.Api.Services;

namespace FiloTakip.Api.Tests;

// AlarmRules'un davranışını kayıt altına alan testler.
// Kuralı değiştirirsek ya da başka bir yere taşırsak, bir şeyi bozup bozmadığımızı bu testler söyler.
public class AlarmRulesTests
{
    private const short Limit = 90;   // tolerans 5 km/s olduğu için alarm eşiği 95
    private static readonly DateTime Now = new(2026, 10, 31, 12, 0, 0, DateTimeKind.Utc);

    // decimal, [InlineData] içinde yazılamadığı için hızları double verip çeviriyoruz
    [Theory]
    [InlineData(90, false)]    // tam sınırda
    [InlineData(95, false)]    // tolerans içinde (sınır + 5)
    [InlineData(95.1, true)]   // toleransı aştı
    [InlineData(130, true)]
    public void IsSpeeding_UsesTolerance(double speedKmh, bool expected)
    {
        Assert.Equal(expected, AlarmRules.IsSpeeding(Limit, (decimal)speedKmh));
    }

    [Fact]
    public void ShouldCreateSpeedAlarm_NewViolation_ReturnsTrue()
    {
        var result = AlarmRules.ShouldCreateSpeedAlarm(
            Limit, previousSpeedKmh: 80, currentSpeedKmh: 100, lastSpeedAlarmAt: null, Now);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCreateSpeedAlarm_FirstLocationOfVehicle_ReturnsTrue()
    {
        var result = AlarmRules.ShouldCreateSpeedAlarm(
            Limit, previousSpeedKmh: null, currentSpeedKmh: 100, lastSpeedAlarmAt: null, Now);

        Assert.True(result);
    }

    [Fact]
    public void ShouldCreateSpeedAlarm_WithinTolerance_ReturnsFalse()
    {
        var result = AlarmRules.ShouldCreateSpeedAlarm(
            Limit, previousSpeedKmh: 80, currentSpeedKmh: 93, lastSpeedAlarmAt: null, Now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldCreateSpeedAlarm_ViolationContinues_ReturnsFalse()
    {
        var result = AlarmRules.ShouldCreateSpeedAlarm(
            Limit, previousSpeedKmh: 100, currentSpeedKmh: 105, lastSpeedAlarmAt: null, Now);

        Assert.False(result);
    }

    [Fact]
    public void ShouldCreateSpeedAlarm_PreviousOverLimitButWithinTolerance_ReturnsTrue()
    {
        // Önceki hız (94) sınırın üstünde ama tolerans içinde: ihlal sayılmaz, yani bu yeni bir ihlal
        var result = AlarmRules.ShouldCreateSpeedAlarm(
            Limit, previousSpeedKmh: 94, currentSpeedKmh: 100, lastSpeedAlarmAt: null, Now);

        Assert.True(result);
    }

    [Theory]
    [InlineData(1, false)]    // 1 dakika önce alarm verilmiş: bekleme süresi dolmadı
    [InlineData(4, false)]
    [InlineData(5, true)]     // tam 5 dakika: süre doldu
    [InlineData(30, true)]
    public void ShouldCreateSpeedAlarm_RespectsCooldown(int minutesSinceLastAlarm, bool expected)
    {
        var result = AlarmRules.ShouldCreateSpeedAlarm(
            Limit, previousSpeedKmh: 80, currentSpeedKmh: 100,
            lastSpeedAlarmAt: Now.AddMinutes(-minutesSinceLastAlarm), Now);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(93.5, "Hız sınırı aşıldı: 93,5 km/s (sınır 90)")]   // Türkçe ondalık virgül
    [InlineData(104, "Hız sınırı aşıldı: 104 km/s (sınır 90)")]     // tam sayıda ",0" yazılmaz
    public void SpeedViolationMessage_IsTurkishFormatted(double speedKmh, string expected)
    {
        Assert.Equal(expected, AlarmRules.SpeedViolationMessage(Limit, (decimal)speedKmh));
    }

    // ---------- Düşük yakıt (eşik %15) ----------

    [Theory]
    [InlineData(15.1, 14.9, true)]    // eşiğin altına yeni indi
    [InlineData(15, 14.9, true)]      // tam %15 düşük sayılmaz, yani önceki konumda yakıt yeterliydi
    [InlineData(14.9, 14.5, false)]   // zaten düşüktü: depo dolana kadar her konumda alarm olmasın
    [InlineData(20, 15, false)]       // tam eşikte: henüz düşük değil
    [InlineData(5, 100, false)]       // depo doldu
    public void ShouldCreateLowFuelAlarm_OnlyWhenFallingBelowThreshold(double previous, double current, bool expected)
    {
        var result = AlarmRules.ShouldCreateLowFuelAlarm((decimal)previous, (decimal)current, lastLowFuelAlarmAt: null, Now);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldCreateLowFuelAlarm_FirstLocationOfVehicle_ReturnsTrue()
    {
        var result = AlarmRules.ShouldCreateLowFuelAlarm(previousFuelPercent: null, 8, lastLowFuelAlarmAt: null, Now);

        Assert.True(result);
    }

    [Theory]
    [InlineData(3, false)]    // sensör dalgalandı (%14,9 -> %15,2 -> %14,8): 3 dakika önce zaten alarm verildi
    [InlineData(10, true)]    // tam 10 dakika: süre doldu
    public void ShouldCreateLowFuelAlarm_RespectsCooldown(int minutesSinceLastAlarm, bool expected)
    {
        var result = AlarmRules.ShouldCreateLowFuelAlarm(
            15.2m, 14.8m, lastLowFuelAlarmAt: Now.AddMinutes(-minutesSinceLastAlarm), Now);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(8.4, "Yakıt seviyesi düşük: %8,4")]
    [InlineData(14, "Yakıt seviyesi düşük: %14")]
    public void LowFuelMessage_IsTurkishFormatted(double fuelPercent, string expected)
    {
        Assert.Equal(expected, AlarmRules.LowFuelMessage((decimal)fuelPercent));
    }

    // ---------- Yakıt hırsızlığı (araç dururken 5 puan düşüş) ----------

    [Theory]
    [InlineData(0, true)]
    [InlineData(2.9, true)]    // GPS dalgalanması: duran araç 1-2 km/s gösterebilir
    [InlineData(3, false)]
    [InlineData(40, false)]
    public void IsStationary_AllowsGpsNoise(double speedKmh, bool expected)
    {
        Assert.Equal(expected, AlarmRules.IsStationary((decimal)speedKmh));
    }

    [Theory]
    [InlineData(62, 61, false)]     // sensör dalgalanması
    [InlineData(62, 57.1, false)]   // 4,9 puan: eşiğin altında
    [InlineData(62, 57, true)]      // tam 5 puan
    [InlineData(62, 40, true)]
    [InlineData(62, 70, false)]     // yakıt arttı (depo dolduruldu)
    public void IsFuelDrain_UsesThreshold(double fuelWhenStopped, double current, bool expected)
    {
        Assert.Equal(expected, AlarmRules.IsFuelDrain((decimal)fuelWhenStopped, (decimal)current));
    }

    [Fact]
    public void FuelDrainMessage_IsTurkishFormatted()
    {
        Assert.Equal("Araç dururken yakıt düştü: %62,5 → %44 (hırsızlık şüphesi)", AlarmRules.FuelDrainMessage(62.5m, 44m));
    }
}
