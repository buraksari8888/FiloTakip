namespace FiloTakip.Api.Dtos;

// Haritada çizilecek bölge (GET /api/geofences)
public record GeofenceDto(
    int GeofenceId,
    string Name,
    decimal Latitude,
    decimal Longitude,
    int RadiusMeters,
    bool AlarmOnEnter,
    bool AlarmOnExit);
