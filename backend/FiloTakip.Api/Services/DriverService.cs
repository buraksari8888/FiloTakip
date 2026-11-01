using System.Linq.Expressions;
using FiloTakip.Api.Data;
using FiloTakip.Api.Dtos;
using FiloTakip.Api.Hubs;
using FiloTakip.Api.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Services;

// Sürücülerle ilgili işlemler: listeleme, ekleme, ad düzeltme
public class DriverService(
    FiloTakipDbContext db,
    IHubContext<LiveHub, ILiveClient> live,
    ILogger<DriverService> logger)
{
    // Driver -> DriverDto dönüşümü tek yerde; aracın plakası JOIN ile gelir
    private static readonly Expression<Func<Driver, DriverDto>> ToDto = d => new DriverDto(
        d.DriverId,
        d.FirstName,
        d.LastName,
        d.Phone,
        d.LicenseNumber,
        d.Vehicles.Where(v => v.IsActive).Select(v => v.Plate).FirstOrDefault());

    // Aktif sürücüler, ada göre sıralı; her birinin şu an kullandığı aktif araçla
    public Task<List<DriverDto>> GetAllAsync(CancellationToken cancellationToken) =>
        db.Drivers
            .Where(d => d.IsActive)
            .OrderBy(d => d.FirstName)
            .ThenBy(d => d.LastName)
            .Select(ToDto)
            .ToListAsync(cancellationToken);

    // Sürücü yoksa (ya da pasifse) null döner
    public Task<DriverDto?> GetByIdAsync(int driverId, CancellationToken cancellationToken) =>
        db.Drivers
            .Where(d => d.DriverId == driverId && d.IsActive)
            .Select(ToDto)
            .FirstOrDefaultAsync(cancellationToken);

    // Yeni sürücü ekler. Ehliyet numarası başka bir sürücüde varsa eklemez, null döner.
    public async Task<DriverDto?> CreateAsync(CreateDriverDto dto, CancellationToken cancellationToken)
    {
        var licenseNumber = VehicleRules.CleanText(dto.LicenseNumber).ToUpperInvariant();
        if (await db.Drivers.AnyAsync(d => d.LicenseNumber == licenseNumber, cancellationToken))
            return null;

        var driver = new Driver
        {
            FirstName = VehicleRules.CleanText(dto.FirstName),
            LastName = VehicleRules.CleanText(dto.LastName),
            Phone = string.IsNullOrWhiteSpace(dto.Phone) ? null : dto.Phone.Trim(),
            LicenseNumber = licenseNumber
        };
        db.Drivers.Add(driver);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException e) when (DbErrors.IsUniqueViolation(e, "UQ_Drivers_LicenseNumber"))
        {
            return null;   // aynı ehliyet numarası az önce başka bir istekle eklendi
        }
        // Ad loga yazılmıyor: kişisel bilgi, numarası yeterli
        logger.LogInformation("Sürücü #{DriverId} eklendi", driver.DriverId);
        return new DriverDto(driver.DriverId, driver.FirstName, driver.LastName, driver.Phone, driver.LicenseNumber, null);
    }

    // Sürücünün adını düzeltir. Sürücü yoksa false döner.
    public async Task<bool> UpdateNameAsync(int driverId, UpdateDriverNameDto dto, CancellationToken cancellationToken)
    {
        var driver = await db.Drivers.FirstOrDefaultAsync(d => d.DriverId == driverId && d.IsActive, cancellationToken);
        if (driver is null)
            return false;

        driver.FirstName = VehicleRules.CleanText(dto.FirstName);
        driver.LastName = VehicleRules.CleanText(dto.LastName);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Sürücü #{DriverId} adı düzeltildi", driverId);
        await live.Clients.All.VehiclesChanged();   // araç listesindeki sürücü adı güncellensin
        return true;
    }
}
