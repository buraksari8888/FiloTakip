namespace FiloTakip.Api.Dtos;

// API'nin dışarıya döndürdüğü araç bilgisi.
// Veritabanı modelini (Vehicle) doğrudan döndürmek yerine sadece gereken alanları seçiyoruz.
public record VehicleDto(
    int VehicleId,
    string Plate,
    string Brand,
    string Model,
    short ModelYear,
    short SpeedLimitKmh,
    bool IsActive,
    int? DriverId,
    string? DriverName);
