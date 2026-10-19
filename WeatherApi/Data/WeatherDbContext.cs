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
        // Mapear os nomes das tabelas para minúsculas,
        // que é como o MySQL os guarda no Windows e como já existem no backup.
        modelBuilder.Entity<User>().ToTable("users");
        modelBuilder.Entity<Device>().ToTable("devices");
        modelBuilder.Entity<DeviceReading>().ToTable("devicereadings");
        modelBuilder.Entity<TemperatureReading>().ToTable("temperaturereadings");
        modelBuilder.Entity<UserQuery>().ToTable("userqueries");

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