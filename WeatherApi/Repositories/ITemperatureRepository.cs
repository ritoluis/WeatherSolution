using WeatherApi.Entities;

namespace WeatherApi.Repositories;

public interface ITemperatureRepository
{
    Task<TemperatureReading?> GetByDateAsync(DateOnly date);
    Task<IEnumerable<TemperatureReading>> GetRangeAsync(DateOnly from, DateOnly to);
    Task AddAsync(TemperatureReading reading);
    Task SaveChangesAsync();
}