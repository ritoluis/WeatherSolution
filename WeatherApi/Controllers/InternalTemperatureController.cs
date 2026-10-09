using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherApi.DTOs;
using WeatherApi.Entities;
using WeatherApi.Repositories;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/internal/temperature")]
[Authorize(Policy = "InternalClient")]
public class InternalTemperatureController : ControllerBase
{
    private readonly ITemperatureService _service;
    private readonly IUserRepository _users;

    public InternalTemperatureController(ITemperatureService service, IUserRepository users)
    {
        _service = service;
        _users = users;
    }

    [HttpGet("by-date/{date}")]
    public async Task<IActionResult> GetByDate(DateOnly date)
    {
        var temp = await _service.GetByDateAsync(date);
        if (temp == null) return NotFound();

        await LogQueryAsync(date, $"GET /internal/temperature/by-date/{date}");
        return Ok(new TemperatureResponse(date, temp));
    }

    [HttpGet("range")]
    public async Task<IActionResult> GetRange([FromQuery] DateOnly from, [FromQuery] DateOnly to)
    {
        var results = await _service.GetRangeAsync(from, to);
        return Ok(results);
    }

    private async Task LogQueryAsync(DateOnly targetDate, string endpoint)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId)) return;

        await _users.AddQueryAsync(new UserQuery
        {
            UserId = userId,
            QueryDate = DateTime.UtcNow,
            Endpoint = endpoint,
            TargetDate = targetDate
        });
        await _users.SaveChangesAsync();
    }

    [HttpGet("yesterday")]
    public async Task<IActionResult> GetYesterday()
    {
        var temp = await _service.GetYesterdayAverageAsync();
        if (temp == null)
            return NotFound(new { message = "Sem registo para o dia anterior" });

        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        await LogQueryAsync(yesterday, "GET /internal/temperature/yesterday");
        return Ok(new TemperatureResponse(yesterday, temp));
    }
}