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
        if (temp == null) return NotFound(new { message = "Sem registo para ontem" });

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        return Ok(new TemperatureResponse(yesterday, temp));
    }
}