using WeatherApi.Services;

namespace WeatherApi.Workers;

public class DailyTemperatureWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DailyTemperatureWorker> _logger;

    public DailyTemperatureWorker(IServiceProvider services, ILogger<DailyTemperatureWorker> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ITemperatureService>();
            try
            {
                await service.RegisterDailyAverageAsync();
                _logger.LogInformation("Registo diário criado às {Time}", DateTime.Now);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar registo diário");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}