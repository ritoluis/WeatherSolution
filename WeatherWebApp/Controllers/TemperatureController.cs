using Microsoft.AspNetCore.Mvc;
using WeatherWebApp.Models;
using WeatherWebApp.Services;

namespace WeatherWebApp.Controllers;

public class TemperatureController : Controller
{
    private readonly IApiClient _api;
    public TemperatureController(IApiClient api) => _api = api;

    private string? Token => HttpContext.Session.GetString("Token");

    public async Task<IActionResult> Index(DateOnly? date)
    {
        if (Token == null) return RedirectToAction("Login", "Account");

        var isAdmin = HttpContext.Session.GetString("Role") == "Admin";
        var model = new TemperatureViewModel { IsAdmin = isAdmin };

        if (date.HasValue)
        {
            // Utilizador escolheu uma data: consulta essa data
            var result = await _api.GetByDateAsync(Token, date.Value);
            if (result == null)
            {
                model.Message = $"Sem registo para {date.Value:yyyy-MM-dd}";
                model.Date = date.Value;
            }
            else
            {
                model.Date = result.Date;
                model.AverageTemperature = result.AverageTemperature;
            }
        }
        else
        {
            // Sem data: mostra o dia anterior por defeito
            var result = await _api.GetYesterdayAsync(Token);
            if (result == null)
            {
                model.Message = "Sem registo para o dia anterior";
            }
            else
            {
                model.Date = result.Date;
                model.AverageTemperature = result.AverageTemperature;
            }
        }

        if (isAdmin)
            model.Queries = await _api.GetAdminQueriesAsync(Token) ?? new();

        return View(model);
    }
}