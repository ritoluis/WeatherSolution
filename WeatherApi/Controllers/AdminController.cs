using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherApi.DTOs;
using WeatherApi.Entities;
using WeatherApi.Repositories;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IUserRepository users, ILogger<AdminController> logger)
    {
        _users = users;
        _logger = logger;
    }

    // ============ Auditoria ============
    [HttpGet("queries")]
    public async Task<IActionResult> GetAllQueries([FromQuery] string? username = null)
    {
        var queries = await _users.GetAllQueriesAsync();

        if (!string.IsNullOrEmpty(username))
            queries = queries.Where(q => q.User?.Username == username);

        return Ok(queries.Select(q => new
        {
            q.Id,
            Username = q.User?.Username,
            Role = q.User?.Role,
            q.QueryDate,
            q.Endpoint,
            q.TargetDate
        }));
    }

    // ============ Criar utilizador interno ============
    [HttpPost("users/internal")]
    public async Task<IActionResult> CreateInternalUser([FromBody] CreateUserRequest req)
    {
        var existing = await _users.GetByUsernameAsync(req.Username);
        if (existing != null)
            return Conflict(new { message = "Username já existe" });

        var user = new User
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "User",   // interno, mas não admin
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user);
        await _users.SaveChangesAsync();

        _logger.LogInformation("Utilizador interno criado: {Username} pelo Admin: {Admin}",
            req.Username, User.Identity?.Name);

        return Ok(new { user.Id, user.Username, message = "Utilizador interno criado" });
    }

    // ============ Listar utilizadores ============
    [HttpGet("users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await _users.GetAllAsync();
        return Ok(users.Select(u => new
        {
            u.Id,
            u.Username,
            u.Role,
            u.CreatedAt
        }));
    }
}