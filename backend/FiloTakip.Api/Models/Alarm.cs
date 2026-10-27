namespace FiloTakip.Api.Models;

// dbo.Alarms tablosunun C# karşılığı
public class Alarm
{
    public long AlarmId { get; set; }
    public int VehicleId { get; set; }
    public long? LocationId { get; set; }
    public string AlarmType { get; set; } = "";     // 'SpeedLimit', 'LowFuel', 'Idle'
    public string Message { get; set; } = "";
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Vehicle? Vehicle { get; set; }
    public LocationRecord? Location { get; set; }
}
