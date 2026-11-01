namespace FiloTakip.Api.Models;

// dbo.Vehicles tablosunun C# karşılığı
public class Vehicle
{
    public int VehicleId { get; set; }
    public string Plate { get; set; } = "";
    public string VehicleType { get; set; } = VehicleTypes.Car;
    public string Brand { get; set; } = "";
    public string Model { get; set; } = "";
    public short ModelYear { get; set; }            // SMALLINT -> short
    public short SpeedLimitKmh { get; set; } = 90;
    public int? DriverId { get; set; }              // aracın sürücüsü olmayabilir
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // İlişkiler: EF Core bunları FOREIGN KEY'lere bakarak doldurur
    public Driver? Driver { get; set; }
    public List<LocationRecord> LocationRecords { get; set; } = [];
    public List<Alarm> Alarms { get; set; } = [];
}

// Veritabanındaki CK_Vehicles_VehicleType kuralının izin verdiği araç türleri
public static class VehicleTypes
{
    public const string Car = "Otomobil";
    public const string LightCommercial = "HafifTicari";   // kamyonet, panelvan (Transit, Doblo...)
    public const string Truck = "Kamyon";                  // kamyon ve tır (çekici)
    public const string Bus = "Otobus";

    public static readonly string[] All = [Car, LightCommercial, Truck, Bus];
}
