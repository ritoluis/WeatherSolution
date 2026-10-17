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
            var result = await _api.GetByDateAsync(Token, date.Value);
            if (result == null)
            {
                model.Message = "Erro ao contactar a API.";
                model.Date = date.Value;
            }
            else
            {
                model.Date = result.Date;
                model.AverageTemperature = result.AverageTemperature;
                model.Message = result.Message;
            }
        }
        else
        {
            var result = await _api.GetYesterdayAsync(Token);
            if (result == null)
            {
                model.Message = "Erro ao contactar a API.";
            }
            else
            {
                model.Date = result.Date;
                model.AverageTemperature = result.AverageTemperature;
                model.Message = result.Message;
            }
        }

        if (isAdmin)
            model.Queries = await _api.GetAdminQueriesAsync(Token) ?? new();

        return View(model);
    }
}