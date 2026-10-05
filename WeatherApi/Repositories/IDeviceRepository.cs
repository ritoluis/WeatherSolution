using WeatherApi.Entities;

namespace WeatherApi.Repositories;

public interface IDeviceRepository
{
    Task<List<int>> GetActiveDeviceIdsAsync();
    Task<List<DeviceReading>> GetReadingsByDateAsync(DateOnly date, List<int> deviceIds);
    Task<DeviceReading?> GetReadingAsync(int deviceId, DateOnly date);
    Task AddReadingAsync(DeviceReading reading);
    Task UpdateReadingAsync(DeviceReading reading);
    Task SaveChangesAsync();
    Task DeleteAsync(Device device);
    Task AddAsync(Device device);
    Task<List<Device>> GetAllAsync();
    Task<Device?> GetByIdAsync(int id);
    Task<DeviceReading?> GetLatestReadingAsync(int deviceId);
    Task<List<DeviceReading>> GetReadingsForDeviceAsync(int deviceId, DateOnly from, DateOnly to);
    Task<decimal?> GetAggregateReadingAsync(DateOnly date);
}