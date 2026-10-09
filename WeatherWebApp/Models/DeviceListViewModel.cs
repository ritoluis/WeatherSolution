namespace WeatherWebApp.Models;

public class DeviceListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public DateOnly? LastReadingDate { get; set; }
    public decimal? LastReadingTemperature { get; set; }

    public string StatusLabel => IsActive ? "Ativo" : "Inativo";
    public string StatusCss => IsActive ? "bg-success" : "bg-secondary";
}