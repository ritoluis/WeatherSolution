

namespace WeatherApi.Services;

public interface IMqttService
{
    Task<decimal?> RequestReadingAsync(int deviceId, TimeSpan timeout);
    bool IsConnected { get; }
}
