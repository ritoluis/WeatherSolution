namespace WeatherApi.Entities;

public class DeviceReading
{
    public int Id { get; set; }
    public int DeviceId { get; set; }
    public Device? Device { get; set; }
    public DateOnly ReadingDate { get; set; }
    public decimal AverageTemperature { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
}