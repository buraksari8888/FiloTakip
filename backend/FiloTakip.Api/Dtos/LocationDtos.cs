using System.ComponentModel.DataAnnotations;

namespace FiloTakip.Api.Dtos;

// Araçtan (simülatörden) gelen konum verisi.
// Kurallara uymayan istekler controller'a hiç ulaşmaz, otomatik olarak 400 Bad Request döner.
public record CreateLocationDto(
    [Range(1, int.MaxValue)] int VehicleId,
    [Range(-90, 90)] decimal Latitude,
    [Range(-180, 180)] decimal Longitude,
    [Range(0, 300)] decimal SpeedKmh,
    [Range(0, 100)] decimal? FuelPercent,
    bool IgnitionOn,
    DateTime? RecordedAt);   // cihazın ölçüm anı (UTC); gönderilmezse sunucu saati kullanılır

// API'nin döndürdüğü konum kaydı
public record LocationDto(
    long LocationId,
    int VehicleId,
    decimal Latitude,
    decimal Longitude,
    decimal SpeedKmh,
    decimal? FuelPercent,
    bool IgnitionOn,
    DateTime RecordedAt,
    short? RoadSpeedLimitKmh);   // bulunduğu yolun hız sınırı (API bulur); bilinen bir yolda değilse null
