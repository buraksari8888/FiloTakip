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

// Alarm listesi süzgeci. Boş (null) alanlar süzmez. From/To: alarmın oluştuğu an (UTC) bu aralıkta olsun.
public record AlarmFilter(bool UnreadOnly, int? VehicleId = null, DateTime? From = null, DateTime? To = null);
