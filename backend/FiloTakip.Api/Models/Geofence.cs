using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Models;

// dbo.Geofences tablosunun C# karşılığı: haritada daire şeklinde bir bölge (merkez + yarıçap)
public class Geofence
{
    public int GeofenceId { get; set; }
    public string Name { get; set; } = "";

    [Precision(9, 6)]
    public decimal Latitude { get; set; }

    [Precision(9, 6)]
    public decimal Longitude { get; set; }

    public int RadiusMeters { get; set; }
    public bool AlarmOnEnter { get; set; } = true;  // araç bölgeye girince alarm
    public bool AlarmOnExit { get; set; } = true;   // araç bölgeden çıkınca alarm
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
