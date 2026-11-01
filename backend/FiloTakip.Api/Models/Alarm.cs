namespace FiloTakip.Api.Models;

// dbo.Alarms tablosunun C# karşılığı
public class Alarm
{
    public long AlarmId { get; set; }
    public int VehicleId { get; set; }
    public long? LocationId { get; set; }
    public string AlarmType { get; set; } = "";     // AlarmTypes sınıfındaki değerlerden biri
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vehicle? Vehicle { get; set; }
    public LocationRecord? Location { get; set; }
}

// Veritabanındaki CK_Alarms_Type kuralının izin verdiği değerler.
// Kodda "SpeedLimit" yazısını elle yazmak yerine bunları kullanırız; yazım hatası derlemede yakalanır.
public static class AlarmTypes
{
    public const string SpeedLimit = "SpeedLimit";
    public const string LowFuel = "LowFuel";
    public const string Idle = "Idle";
    public const string FuelDrain = "FuelDrain";   // araç dururken yakıt düştü (hırsızlık şüphesi)
    public const string GeofenceEnter = "GeofenceEnter";   // araç bir bölgeye girdi
    public const string GeofenceExit = "GeofenceExit";     // araç bir bölgeden çıktı
}
