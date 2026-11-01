using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Models;

// dbo.RoadSegments tablosunun C# karşılığı: yolun iki noktası arasındaki düz parça ve o yolun hız sınırı
public class RoadSegment
{
    public int RoadSegmentId { get; set; }
    public string? RoadName { get; set; }
    public string HighwayType { get; set; } = "";
    public short SpeedLimitKmh { get; set; }

    [Precision(9, 6)]
    public decimal StartLatitude { get; set; }

    [Precision(9, 6)]
    public decimal StartLongitude { get; set; }

    [Precision(9, 6)]
    public decimal EndLatitude { get; set; }

    [Precision(9, 6)]
    public decimal EndLongitude { get; set; }
}
