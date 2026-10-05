using WeatherApi.DTOs;
using WeatherApi.Entities;
using WeatherApi.Repositories;

namespace WeatherApi.Services;

public class TemperatureService : ITemperatureService
{
    private readonly ITemperatureRepository _repo;
    private readonly IDeviceRepository _deviceRepo;
    public TemperatureService(ITemperatureRepository repo, IDeviceRepository deviceRepo)
    {
        _repo = repo;
        _deviceRepo = deviceRepo;
    }

    public async Task<decimal?> GetYesterdayAverageAsync()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var reading = await _repo.GetByDateAsync(yesterday);
        return reading?.AverageTemperature;
    }

    public async Task<decimal?> GetByDateAsync(DateOnly date)
    {
        var reading = await _repo.GetByDateAsync(date);
        return reading?.AverageTemperature;
    }

    public async Task<IEnumerable<TemperatureResponse>> GetRangeAsync(DateOnly from, DateOnly to)
    {
        var readings = await _repo.GetRangeAsync(from, to);
        return readings.Select(r => new TemperatureResponse(r.ReadingDate, r.AverageTemperature));
    }

    public async Task RegisterDailyAverageAsync()
    {
        var random = new Random();
        var temp = Math.Round((decimal)(random.NextDouble() * 20 + 5), 2);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var existing = await _repo.GetByDateAsync(today);
        if (existing != null) return;

        await _repo.AddAsync(new TemperatureReading
        {
            ReadingDate = today,
            AverageTemperature = temp
        });
        await _repo.SaveChangesAsync();
    }

    public async Task RecalculateDailyAverageAsync(DateOnly date)
    {
        // Vai buscar todos os dispositivos ativos
        var activeDeviceIds = await _deviceRepo.GetActiveDeviceIdsAsync();

        if (!activeDeviceIds.Any())
        {
            // Sem dispositivos ativos: não há média para calcular
            return;
        }

        // Vai buscar as leituras desse dia, apenas de dispositivos ativos
        var readings = await _deviceRepo.GetReadingsByDateAsync(date, activeDeviceIds);

        if (!readings.Any())
            return;

        var average = readings.Average(r => r.AverageTemperature);
        average = Math.Round(average, 2);

        // Atualiza ou cria o registo em TemperatureReadings
        var existing = await _repo.GetByDateAsync(date);
        if (existing != null)
        {
            existing.AverageTemperature = average;
        }
        else
        {
            await _repo.AddAsync(new TemperatureReading
            {
                ReadingDate = date,
                AverageTemperature = average
            });
        }

        await _repo.SaveChangesAsync();
    }
}