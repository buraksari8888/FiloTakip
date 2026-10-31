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
}
