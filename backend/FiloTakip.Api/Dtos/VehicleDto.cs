using System.ComponentModel.DataAnnotations;

namespace FiloTakip.Api.Dtos;

// API'nin dışarıya döndürdüğü araç bilgisi.
// Veritabanı modelini (Vehicle) doğrudan döndürmek yerine sadece gereken alanları seçiyoruz.
public record VehicleDto(
    int VehicleId,
    string Plate,
    string VehicleType,
    string Brand,
    string Model,
    short ModelYear,
    short SpeedLimitKmh,
    bool IsActive,
    int? DriverId,
    string? DriverName);

// Harita için: aracın son konumu (hiç konumu yoksa konum alanları null)
public record VehicleLastLocationDto(
    int VehicleId,
    string Plate,
    string VehicleType,
    string Brand,
    string Model,
    short SpeedLimitKmh,
    int? DriverId,
    string? DriverName,
    decimal? Latitude,
    decimal? Longitude,
    decimal? SpeedKmh,
    decimal? FuelPercent,
    bool? IgnitionOn,
    DateTime? RecordedAt,
    short? RoadSpeedLimitKmh);   // son konumdaki yolun sınırı; geçerli sınır bununla SpeedLimitKmh'den küçük olanı

// Yeni araç. Plaka "34abc123" gibi de yazılabilir, API "34 ABC 123" biçimine çevirir.
// VehicleType: VehicleTypes'taki değerlerden biri. DriverId verilirse sürücü bu araca atanır.
public record CreateVehicleDto(
    [Required, StringLength(15)] string Plate,
    [Required] string VehicleType,
    [Required, StringLength(50)] string Brand,
    [Required, StringLength(50)] string Model,
    [Range(1990, 2100)] short ModelYear,
    [Range(10, 200)] short SpeedLimitKmh,
    int? DriverId);

// Araca sürücü atama; DriverId null ise araç sürücüsüz kalır
public record AssignDriverDto(int? DriverId);
