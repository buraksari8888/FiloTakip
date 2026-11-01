using System.Linq.Expressions;
using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Hubs;
using FiloTakip.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Araç ekleme ya da sürücü atama sonucu: başarılı mı, değilse neden
public enum VehicleChangeResult { Ok, NotFound, PlateTaken, DriverNotFound }

// Araçlarla ilgili işlemler: listeleme, ekleme, pasife alma, sürücü atama
public class VehicleService(
    FiloTakipDbContext db,
    IHubContext<LiveHub, ILiveClient> live,
    ILogger<VehicleService> logger)
{
    // Vehicle -> VehicleDto dönüşümü tek yerde. Expression olarak tutuyoruz ki EF Core bunu
    // SQL'e çevirebilsin: veritabanından sadece gereken sütunlar okunur, sürücü adı JOIN ile gelir.
    private static readonly Expression<Func<Vehicle, VehicleDto>> ToDto = v => new VehicleDto(
        v.VehicleId,
        v.Plate,
        v.VehicleType,
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
                v.VehicleType,
                v.Brand,
                v.Model,
                v.SpeedLimitKmh,
                v.DriverId,
                v.DriverName,
                v.Latitude,
                v.Longitude,
                v.SpeedKmh,
                v.FuelPercent,
                v.IgnitionOn,
                v.RecordedAt,
                v.RoadSpeedLimitKmh))
            .ToListAsync(cancellationToken);

    // Yeni araç ekler. dto.Plate VehicleRules.NormalizePlate'ten geçmiş olmalı.
    // Plaka pasife alınmış bir araçtaysa yeni kayıt açılmaz, o araç yeni bilgilerle yeniden açılır:
    // plaka veritabanında tek (UNIQUE) ve aracın eski konumları, alarmları bu kayda bağlı.
    public async Task<(VehicleChangeResult Result, VehicleDto? Vehicle)> CreateAsync(
        CreateVehicleDto dto, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.Plate == dto.Plate, cancellationToken);
        if (vehicle is { IsActive: true })
            return (VehicleChangeResult.PlateTaken, null);

        var reactivated = vehicle is not null;
        vehicle ??= db.Vehicles.Add(new Vehicle { Plate = dto.Plate }).Entity;
        vehicle.VehicleType = dto.VehicleType;
        vehicle.Brand = VehicleRules.CleanText(dto.Brand);
        vehicle.Model = VehicleRules.CleanText(dto.Model);
        vehicle.ModelYear = dto.ModelYear;
        vehicle.SpeedLimitKmh = dto.SpeedLimitKmh;
        vehicle.IsActive = true;
        if (!await SetDriverAsync(vehicle, dto.DriverId, cancellationToken))
            return (VehicleChangeResult.DriverNotFound, null);

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(reactivated ? "Araç yeniden açıldı: {Plate}" : "Araç eklendi: {Plate}", vehicle.Plate);
        await live.Clients.All.VehiclesChanged();
        return (VehicleChangeResult.Ok, await GetByIdAsync(vehicle.VehicleId, cancellationToken));
    }

    // Aracı pasife alır: listeden, haritadan ve simülatörden kalkar ama konumları ve alarmları silinmez
    // (raporlar ve geçmiş rota bunları kullanır). Sürücüsü boşa çıkar, başka araca atanabilir.
    // Araç yoksa ya da zaten pasifse false döner.
    public async Task<bool> DeactivateAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId && v.IsActive, cancellationToken);
        if (vehicle is null)
            return false;

        vehicle.IsActive = false;
        vehicle.DriverId = null;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Araç pasife alındı: {Plate}", vehicle.Plate);
        await live.Clients.All.VehiclesChanged();
        return true;
    }

    // Araca sürücü atar (driverId null: sürücüsüz)
    public async Task<VehicleChangeResult> AssignDriverAsync(int vehicleId, int? driverId, CancellationToken cancellationToken)
    {
        var vehicle = await db.Vehicles.FirstOrDefaultAsync(v => v.VehicleId == vehicleId && v.IsActive, cancellationToken);
        if (vehicle is null)
            return VehicleChangeResult.NotFound;
        if (!await SetDriverAsync(vehicle, driverId, cancellationToken))
            return VehicleChangeResult.DriverNotFound;

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("{Plate} aracının sürücüsü değişti: #{DriverId}", vehicle.Plate, driverId);
        await live.Clients.All.VehiclesChanged();
        return VehicleChangeResult.Ok;
    }

    // Sürücüyü araca bağlar. Sürücü başka bir araçtaysa oradan alınır: bir sürücü aynı anda tek araç kullanır.
    // Sürücü yoksa ya da pasifse false. KAYDETMEZ: kaydetme işini çağıran yapar (hepsi tek SaveChanges ile).
    private async Task<bool> SetDriverAsync(Vehicle vehicle, int? driverId, CancellationToken cancellationToken)
    {
        if (driverId is int id)
        {
            if (!await db.Drivers.AnyAsync(d => d.DriverId == id && d.IsActive, cancellationToken))
                return false;

            var otherVehicles = await db.Vehicles
                .Where(v => v.DriverId == id && v.VehicleId != vehicle.VehicleId)
                .ToListAsync(cancellationToken);
            foreach (var other in otherVehicles)
                other.DriverId = null;
        }
        vehicle.DriverId = driverId;
        return true;
    }
}
