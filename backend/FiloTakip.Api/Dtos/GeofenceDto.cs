using System.ComponentModel.DataAnnotations;

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

// Yeni bölge ya da bölgeyi düzenleme (POST ve PUT aynı alanları alır). Sınırlar veritabanındaki kurallarla aynı.
// Ad, aktif bölgeler arasında tekrar edemez (alarm mesajında bölgenin adı geçiyor, iki aynı ad karışırdı).
public record SaveGeofenceDto(
    [Required, StringLength(100)] string Name,
    [Range(-90.0, 90.0)] decimal Latitude,
    [Range(-180.0, 180.0)] decimal Longitude,
    [Range(50, 50000)] int RadiusMeters,
    bool AlarmOnEnter,
    bool AlarmOnExit) : IValidatableObject
{
    // Hiçbir geçişte alarm vermeyen bölge işe yaramaz
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!AlarmOnEnter && !AlarmOnExit)
            yield return new ValidationResult("Girişte, çıkışta ya da ikisinde alarm seçilmeli.", [nameof(AlarmOnEnter)]);
    }
}
