using Microsoft.EntityFrameworkCore;
using WeatherApi.Data;
using WeatherApi.Entities;

namespace WeatherApi.Repositories;

public class DeviceRepository : IDeviceRepository
{
    private readonly WeatherDbContext _context;
    public DeviceRepository(WeatherDbContext context) => _context = context;

    public async Task<List<int>> GetActiveDeviceIdsAsync() =>
        await _context.Devices
            .Where(d => d.IsActive)
            .Select(d => d.Id)
            .ToListAsync();

    public async Task<List<DeviceReading>> GetReadingsByDateAsync(DateOnly date, List<int> deviceIds) =>
        await _context.DeviceReadings
            .Where(r => r.ReadingDate == date && deviceIds.Contains(r.DeviceId))
            .ToListAsync();

    public async Task<DeviceReading?> GetReadingAsync(int deviceId, DateOnly date) =>
        await _context.DeviceReadings
            .FirstOrDefaultAsync(r => r.DeviceId == deviceId && r.ReadingDate == date);

    public async Task AddReadingAsync(DeviceReading reading) =>
        await _context.DeviceReadings.AddAsync(reading);

    public async Task UpdateReadingAsync(DeviceReading reading) =>
        _context.DeviceReadings.Update(reading);

    public async Task SaveChangesAsync() => await _context.SaveChangesAsync();
    public async Task<List<Device>> GetAllAsync() =>
    await _context.Devices.OrderBy(d => d.Name).ToListAsync();

    public async Task<Device?> GetByIdAsync(int id) =>
        await _context.Devices.FindAsync(id);

    public async Task<DeviceReading?> GetLatestReadingAsync(int deviceId) =>
        await _context.DeviceReadings
            .Where(r => r.DeviceId == deviceId)
            .OrderByDescending(r => r.ReadingDate)
            .FirstOrDefaultAsync();

    public async Task<List<DeviceReading>> GetReadingsForDeviceAsync(
        int deviceId, DateOnly from, DateOnly to) =>
        await _context.DeviceReadings
            .Where(r => r.DeviceId == deviceId && r.ReadingDate >= from && r.ReadingDate <= to)
            .OrderByDescending(r => r.ReadingDate)
            .ToListAsync();

    public async Task<decimal?> GetAggregateReadingAsync(DateOnly date) =>
        await _context.TemperatureReadings
            .Where(t => t.ReadingDate == date)
            .Select(t => (decimal?)t.AverageTemperature)
            .FirstOrDefaultAsync();

    public async Task AddAsync(Device device) =>
        await _context.Devices.AddAsync(device);

    public async Task DeleteAsync(Device device) =>
        _context.Devices.Remove(device);
}