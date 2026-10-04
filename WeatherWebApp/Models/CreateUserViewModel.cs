using System.ComponentModel.DataAnnotations;

namespace WeatherWebApp.Models;

public class CreateUserViewModel
{
    [Required(ErrorMessage = "Username é obrigatório")]
    [StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password é obrigatória")]
    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required]
    [Compare(nameof(Password), ErrorMessage = "As passwords não coincidem")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Required]
    public string UserType { get; set; } = "internal";
}