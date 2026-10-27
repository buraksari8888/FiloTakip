using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Models;

// dbo.LocationRecords tablosunun C# karşılığı
public class LocationRecord
{
    // Ad "LocationRecordId" olmadığı için EF Core anahtarı kendisi bulamaz, [Key] ile söylüyoruz
    [Key]
    public long LocationId { get; set; }            // BIGINT -> long
    public int VehicleId { get; set; }

    [Precision(9, 6)]
    public decimal Latitude { get; set; }

    [Precision(9, 6)]
    public decimal Longitude { get; set; }

    [Precision(5, 1)]
    public decimal SpeedKmh { get; set; }

    [Precision(5, 2)]
    public decimal? FuelPercent { get; set; }

    public bool IgnitionOn { get; set; }
    public DateTime RecordedAt { get; set; }        // UTC

    public Vehicle? Vehicle { get; set; }
}
