using Microsoft.EntityFrameworkCore;
using WeatherApi.Entities;

namespace WeatherApi.Data;

public class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TemperatureReading> TemperatureReadings => Set<TemperatureReading>();
    public DbSet<UserQuery> UserQueries => Set<UserQuery>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<DeviceReading> DeviceReadings => Set<DeviceReading>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TemperatureReading>()
            .HasIndex(t => t.ReadingDate).IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();

        modelBuilder.Entity<UserQuery>()
            .HasOne(q => q.User)
            .WithMany(u => u.Queries)
            .HasForeignKey(q => q.UserId);

        modelBuilder.Entity<Device>()
            .HasIndex(d => d.Name);

        // Um dispositivo só pode ter uma leitura por dia
        modelBuilder.Entity<DeviceReading>()
            .HasIndex(r => new { r.DeviceId, r.ReadingDate }).IsUnique();

        modelBuilder.Entity<DeviceReading>()
            .HasOne(r => r.Device)
            .WithMany(d => d.Readings)
            .HasForeignKey(r => r.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}