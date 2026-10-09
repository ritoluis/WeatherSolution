namespace WeatherWebApp.Models;

public class RequestReadingResultViewModel
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public decimal? Temperature { get; set; }
    public DateTime? ReadAt { get; set; }
}