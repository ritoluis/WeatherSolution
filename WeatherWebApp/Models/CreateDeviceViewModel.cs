using System.ComponentModel.DataAnnotations;

namespace WeatherWebApp.Models;

public class CreateDeviceViewModel
{
    [Required(ErrorMessage = "Nome é obrigatório")]
    [StringLength(100, MinimumLength = 3)]
    public string Name { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Location { get; set; }
}

