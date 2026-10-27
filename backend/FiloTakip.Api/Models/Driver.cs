namespace FiloTakip.Api.Models;

// dbo.Drivers tablosunun C# karşılığı
public class Driver
{
    public int DriverId { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Phone { get; set; }              // '?' = NULL olabilir
    public string LicenseNumber { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Bu sürücüye atanmış araçlar (Vehicles.DriverId üzerinden)
    public List<Vehicle> Vehicles { get; set; } = [];
}
