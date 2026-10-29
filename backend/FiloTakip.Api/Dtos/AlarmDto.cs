namespace FiloTakip.Api.Dtos;

// API'nin döndürdüğü alarm (konum bilgisi, alarm bir konumla ilişkiliyse dolu gelir)
public record AlarmDto(
    long AlarmId,
    int VehicleId,
    string Plate,
    string AlarmType,
    string Message,
    bool IsRead,
    DateTime CreatedAt,
    decimal? Latitude,
    decimal? Longitude);
