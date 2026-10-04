using Microsoft.EntityFrameworkCore;
using WeatherApi.Data;
using WeatherApi.Entities;

namespace WeatherApi.Repositories;

public class TemperatureRepository : ITemperatureRepository
{
    private readonly WeatherDbContext _context;
    public TemperatureRepository(WeatherDbContext context) => _context = context;

    public async Task<TemperatureReading?> GetByDateAsync(DateOnly date) =>
        await _context.TemperatureReadings.FirstOrDefaultAsync(t => t.ReadingDate == date);

    public async Task<IEnumerable<TemperatureReading>> GetRangeAsync(DateOnly from, DateOnly to) =>
        await _context.TemperatureReadings
            .Where(t => t.ReadingDate >= from && t.ReadingDate <= to)
            .OrderByDescending(t => t.ReadingDate)
            .ToListAsync();

    public async Task AddAsync(TemperatureReading reading) =>
        await _context.TemperatureReadings.AddAsync(reading);

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
}