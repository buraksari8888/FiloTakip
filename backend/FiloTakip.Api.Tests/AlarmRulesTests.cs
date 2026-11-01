using FiloTakip.Api.Models;
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

    // ---------- Rölanti (kontak açık, araç duruyor, 5 dakika) ----------

    [Theory]
    [InlineData(true, 0, true)]
    [InlineData(true, 1.5, true)]    // GPS dalgalanması: yine duruyor sayılır
    [InlineData(true, 30, false)]    // gidiyor
    [InlineData(false, 0, false)]    // kontak kapalı: park halinde, yakıt yanmıyor
    public void IsIdling_NeedsIgnitionOnAndStationary(bool ignitionOn, double speedKmh, bool expected)
    {
        Assert.Equal(expected, AlarmRules.IsIdling(ignitionOn, (decimal)speedKmh));
    }

    [Theory]
    [InlineData(60, false)]     // trafik ışığı
    [InlineData(299, false)]
    [InlineData(300, true)]     // tam 5 dakika
    [InlineData(1800, true)]
    public void IsLongIdle_After5Minutes(int seconds, bool expected)
    {
        Assert.Equal(expected, AlarmRules.IsLongIdle(Now.AddSeconds(-seconds), Now));
    }

    [Fact]
    public void IdleMessage_ShowsWholeMinutes()
    {
        Assert.Equal("Uzun rölanti: motor 5 dakikadır çalışıyor, araç yerinde bekliyor",
            AlarmRules.IdleMessage(TimeSpan.FromSeconds(330)));
    }

    // ---------- Bölge (geofence) ----------

    // Yarıçapı 400 m olan bir bölge. Enlemde (kuzey-güney) 0,001 derece yaklaşık 111 metre.
    private static Geofence Zone(bool alarmOnEnter = true, bool alarmOnExit = true) => new()
    {
        Name = "Depo",
        Latitude = 41.0m,
        Longitude = 29.0m,
        RadiusMeters = 400,
        AlarmOnEnter = alarmOnEnter,
        AlarmOnExit = alarmOnExit
    };

    [Fact]
    public void DistanceMeters_OneThousandthDegreeLatitude_IsAbout111Meters()
    {
        Assert.Equal(111.2, GeoMath.DistanceMeters(41.0m, 29.0m, 41.001m, 29.0m), precision: 1);
    }

    [Theory]
    [InlineData(41.0, true)]      // tam merkezde
    [InlineData(41.0035, true)]   // ~389 m: sınırın hemen içinde
    [InlineData(41.0037, false)]  // ~411 m: sınırın hemen dışında
    [InlineData(41.01, false)]    // ~1,1 km
    public void IsInsideGeofence_ComparesDistanceWithRadius(double latitude, bool expected)
    {
        Assert.Equal(expected, AlarmRules.IsInsideGeofence(Zone(), (decimal)latitude, 29.0m));
    }

    [Theory]
    [InlineData(false, true, AlarmTypes.GeofenceEnter)]   // dışarıdan içeri: girdi
    [InlineData(true, false, AlarmTypes.GeofenceExit)]    // içeriden dışarı: çıktı
    [InlineData(true, true, null)]                        // içeride kaldı: yeni alarm yok
    [InlineData(false, false, null)]                      // dışarıda kaldı
    public void GeofenceAlarmType_OnlyOnBoundaryCrossing(bool wasInside, bool isInside, string? expected)
    {
        Assert.Equal(expected, AlarmRules.GeofenceAlarmType(Zone(), wasInside, isInside));
    }

    [Fact]
    public void GeofenceAlarmType_ExitOnlyZone_IgnoresEntering()
    {
        var depot = Zone(alarmOnEnter: false, alarmOnExit: true);

        Assert.Null(AlarmRules.GeofenceAlarmType(depot, wasInside: false, isInside: true));
        Assert.Equal(AlarmTypes.GeofenceExit, AlarmRules.GeofenceAlarmType(depot, wasInside: true, isInside: false));
    }

    [Fact]
    public void GeofenceAlarmType_EnterOnlyZone_IgnoresExiting()
    {
        var customer = Zone(alarmOnEnter: true, alarmOnExit: false);

        Assert.Equal(AlarmTypes.GeofenceEnter, AlarmRules.GeofenceAlarmType(customer, wasInside: false, isInside: true));
        Assert.Null(AlarmRules.GeofenceAlarmType(customer, wasInside: true, isInside: false));
    }

    [Fact]
    public void GeofenceMessage_NamesTheZone()
    {
        Assert.Equal("Bölgeye girdi: Yeşilköy Depo", AlarmRules.GeofenceMessage(AlarmTypes.GeofenceEnter, "Yeşilköy Depo"));
        Assert.Equal("Bölgeden çıktı: Yeşilköy Depo", AlarmRules.GeofenceMessage(AlarmTypes.GeofenceExit, "Yeşilköy Depo"));
    }
}
