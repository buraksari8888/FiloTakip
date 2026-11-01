using FiloTakip.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FiloTakip.Api.Data;

// Veritabanının C# tarafındaki temsili. Her DbSet bir tabloya karşılık gelir.
public class FiloTakipDbContext(DbContextOptions<FiloTakipDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<LocationRecord> LocationRecords => Set<LocationRecord>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
    public DbSet<Geofence> Geofences => Set<Geofence>();
    public DbSet<VehicleLastLocation> VehicleLastLocations => Set<VehicleLastLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // View: anahtarı yok, sadece okunur
        modelBuilder.Entity<VehicleLastLocation>()
            .HasNoKey()
            .ToView("vw_VehicleLastLocation");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Veritabanındaki bütün tarihler UTC. Okurken bunu belirtmezsek JSON'a "Z" olmadan çıkar
        // ve tarayıcı yerel saat sanıp 3 saat kaydırır.
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    private class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        toDb => toDb,
        fromDb => DateTime.SpecifyKind(fromDb, DateTimeKind.Utc));
}
