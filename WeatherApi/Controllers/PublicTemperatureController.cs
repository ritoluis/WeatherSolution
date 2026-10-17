using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherApi.DTOs;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/public/temperature")]
[Authorize(Policy = "ExternalClient")]
public class PublicTemperatureController : ControllerBase
{
    private readonly ITemperatureService _service;
    public PublicTemperatureController(ITemperatureService service) => _service = service;

    // Agentes externos só podem consultar o dia anterior
    [HttpGet("yesterday")]
    public async Task<IActionResult> GetYesterday()
    {
        var temp = await _service.GetYesterdayAverageAsync();
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));

        if (temp == null)
        {
            return NotFound(new
            {
                date = yesterday,
                message = $"Sem leitura registada para {yesterday:yyyy-MM-dd}."
            });
        }

        return Ok(new TemperatureResponse(yesterday, temp));
    }
}