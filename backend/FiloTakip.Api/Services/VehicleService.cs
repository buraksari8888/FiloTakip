using System.Linq.Expressions;
using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Araçlarla ilgili okuma işlemleri
public class VehicleService(FiloTakipDbContext db)
{
    // Vehicle -> VehicleDto dönüşümü tek yerde. Expression olarak tutuyoruz ki EF Core bunu
    // SQL'e çevirebilsin: veritabanından sadece gereken sütunlar okunur, sürücü adı JOIN ile gelir.
    private static readonly Expression<Func<Vehicle, VehicleDto>> ToDto = v => new VehicleDto(
        v.VehicleId,
        v.Plate,
        v.Brand,
        v.Model,
        v.ModelYear,
        v.SpeedLimitKmh,
        v.IsActive,
        v.DriverId,
        v.Driver == null ? null : v.Driver.FirstName + " " + v.Driver.LastName);

    public Task<List<VehicleDto>> GetAllAsync(CancellationToken cancellationToken) =>
        db.Vehicles
            .OrderBy(v => v.Plate)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    // Araç yoksa null döner
    public Task<VehicleDto?> GetByIdAsync(int vehicleId, CancellationToken cancellationToken) =>
        db.Vehicles
            .Where(v => v.VehicleId == vehicleId)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    // Her aktif aracın son konumu (vw_VehicleLastLocation view'ından)
    public Task<List<VehicleLastLocationDto>> GetLastLocationsAsync(CancellationToken cancellationToken) =>
        db.VehicleLastLocations
            .OrderBy(v => v.Plate)
            .Select(v => new VehicleLastLocationDto(
                v.VehicleId,
                v.Plate,
                v.Brand,
                v.Model,
                v.SpeedLimitKmh,
                v.DriverName,
                v.Latitude,
                v.Longitude,
                v.SpeedKmh,
                v.FuelPercent,
                v.IgnitionOn,
                v.RecordedAt))
            .ToListAsync(cancellationToken);
}
