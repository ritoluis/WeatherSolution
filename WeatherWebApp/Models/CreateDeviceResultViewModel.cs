namespace WeatherWebApp.Models;

public class CreateDeviceResultViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string ApiKey { get; set; } = string.Empty;
}