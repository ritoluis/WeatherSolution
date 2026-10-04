namespace WeatherWebApp.Models;
public class QueryLogViewModel
{
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;   // adicionar
    public DateTime QueryDate { get; set; }
    public string Endpoint { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
}