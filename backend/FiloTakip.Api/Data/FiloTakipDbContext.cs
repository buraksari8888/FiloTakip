using FiloTakip.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FiloTakip.Api.Data;

// Veritabanının C# tarafındaki temsili. Her DbSet bir tabloya karşılık gelir.
public class FiloTakipDbContext(DbContextOptions<FiloTakipDbContext> options) : DbContext(options)
{
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<LocationRecord> LocationRecords => Set<LocationRecord>();
    public DbSet<Alarm> Alarms => Set<Alarm>();
}
