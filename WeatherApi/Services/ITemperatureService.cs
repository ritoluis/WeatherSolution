namespace WeatherApi.Services;

public interface ITemperatureService
{
    Task<decimal?> GetYesterdayAverageAsync();
    Task<decimal?> GetByDateAsync(DateOnly date);
    Task<IEnumerable<DTOs.TemperatureResponse>> GetRangeAsync(DateOnly from, DateOnly to);
    Task RegisterDailyAverageAsync();
}