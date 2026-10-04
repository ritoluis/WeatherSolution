namespace WeatherApi.Entities;

public class TemperatureReading
{
    public int Id { get; set; }
    public DateOnly ReadingDate { get; set; }
    public decimal AverageTemperature { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}