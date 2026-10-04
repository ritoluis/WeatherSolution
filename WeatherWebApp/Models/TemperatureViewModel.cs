namespace WeatherWebApp.Models;

public class TemperatureViewModel
{
    public DateOnly Date { get; set; }
    public decimal? AverageTemperature { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsAdmin { get; set; }
    public List<QueryLogViewModel> Queries { get; set; } = new();
}
