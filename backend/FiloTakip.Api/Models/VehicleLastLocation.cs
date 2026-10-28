using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Models;

// dbo.vw_VehicleLastLocation view'ının C# karşılığı (sadece okunur, anahtarı yok).
// Henüz konumu olmayan araçlarda konum alanları NULL gelir.
public class VehicleLastLocation
{
    public int VehicleId { get; set; }
    public string Plate { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public short SpeedLimitKmh { get; set; }
    public string? DriverName { get; set; }

    [Precision(9, 6)]
    public decimal? Latitude { get; set; }

    [Precision(9, 6)]
    public decimal? Longitude { get; set; }

    [Precision(5, 1)]
    public decimal? SpeedKmh { get; set; }

    [Precision(5, 2)]
    public decimal? FuelPercent { get; set; }

    public bool? IgnitionOn { get; set; }
    public DateTime? RecordedAt { get; set; }
}
