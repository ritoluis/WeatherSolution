namespace WeatherWebApp.Models;

public class DeviceDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }

    public List<DeviceReadingViewModel> Readings { get; set; } = new();
}

public class DeviceReadingViewModel
{
    public DateOnly ReadingDate { get; set; }
    public decimal AverageTemperature { get; set; }
    public DateTime ReceivedAt { get; set; }
}