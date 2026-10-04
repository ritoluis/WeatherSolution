using Microsoft.EntityFrameworkCore;
using WeatherApi.Entities;

namespace WeatherApi.Data;

public class WeatherDbContext : DbContext
{
    public WeatherDbContext(DbContextOptions<WeatherDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<TemperatureReading> TemperatureReadings => Set<TemperatureReading>();
    public DbSet<UserQuery> UserQueries => Set<UserQuery>();

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

        base.OnModelCreating(modelBuilder);
    }
}