using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherApi.DTOs;
using WeatherApi.Entities;
using WeatherApi.Repositories;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly ITokenService _tokens;
    private readonly ILoginAttemptService _loginAttempts;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository users,
        ITokenService tokens,
        ILoginAttemptService loginAttempts,
        ILogger<AuthController> logger)
    {
        _users = users;
        _tokens = tokens;
        _loginAttempts = loginAttempts;
        _logger = logger;
    }

    // ============ Login Interno (App Web) ============
    [HttpPost("login/internal")]
    public async Task<IActionResult> LoginInternal([FromBody] LoginRequest req)
    {
        if (_loginAttempts.IsLockedOut(req.Username))
        {
            _logger.LogWarning("Login interno bloqueado para: {Username} do IP: {Ip}",
                req.Username, HttpContext.Connection.RemoteIpAddress);
            return StatusCode(423, new
            {
                message = "Conta bloqueada temporariamente. Tente novamente em 5 minutos."
            });
        }

        var user = await _users.GetByUsernameAsync(req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            _loginAttempts.RecordFailedAttempt(req.Username);
            var remaining = _loginAttempts.GetRemainingAttempts(req.Username);
            _logger.LogWarning("Tentativa de login interna falhada para username: {Username} do IP: {Ip}",
                req.Username, HttpContext.Connection.RemoteIpAddress);

            return Unauthorized(new
            {
                message = "Credenciais inválidas",
                remainingAttempts = remaining
            });
        }

        // Utilizadores externos NÃO podem entrar na app web
        if (user.UserType != "Internal")
        {
            _logger.LogWarning(
                "Tentativa de login interno com conta externa: {Username}", req.Username);
            return StatusCode(403, new
            {
                message = "Esta conta é externa e não pode aceder à aplicação web."
            });
        }

        _loginAttempts.ResetAttempts(req.Username);
        var token = _tokens.GenerateToken(user, "internal");

        _logger.LogInformation("Login interno bem-sucedido: {Username}", user.Username);
        return Ok(new LoginResponse(token, user.Username, user.Role));
    }

    // ============ Login Externo (Agentes) ============
    [HttpPost("login/external")]
    public async Task<IActionResult> LoginExternal([FromBody] LoginRequest req)
    {
        if (_loginAttempts.IsLockedOut(req.Username))
        {
            _logger.LogWarning("Login externo bloqueado para: {Username} do IP: {Ip}",
                req.Username, HttpContext.Connection.RemoteIpAddress);
            return StatusCode(423, new
            {
                message = "Conta bloqueada temporariamente. Tente novamente em 5 minutos."
            });
        }

        var user = await _users.GetByUsernameAsync(req.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
        {
            _loginAttempts.RecordFailedAttempt(req.Username);
            var remaining = _loginAttempts.GetRemainingAttempts(req.Username);
            _logger.LogWarning("Tentativa de login externa falhada para username: {Username} do IP: {Ip}",
                req.Username, HttpContext.Connection.RemoteIpAddress);

            return Unauthorized(new
            {
                message = "Credenciais inválidas",
                remainingAttempts = remaining
            });
        }

        // Utilizadores internos NÃO devem usar o endpoint externo
        if (user.UserType != "External")
        {
            _logger.LogWarning(
                "Tentativa de login externo com conta interna: {Username}", req.Username);
            return StatusCode(403, new
            {
                message = "Esta conta é interna e não pode aceder à API pública."
            });
        }

        _loginAttempts.ResetAttempts(req.Username);
        var token = _tokens.GenerateToken(user, "external");

        _logger.LogInformation("Login externo bem-sucedido: {Username}", user.Username);
        return Ok(new LoginResponse(token, user.Username, user.Role));
    }

    // ============ Criação de Conta Externa (apenas Admin) ============
    [HttpPost("create-external")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateExternalUser([FromBody] CreateUserRequest req)
    {
        var existing = await _users.GetByUsernameAsync(req.Username);
        if (existing != null)
            return Conflict(new { message = "Username já existe" });

        var user = new User
        {
            Username = req.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            Role = "User",
            UserType = "External",       // ← NOVO
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user);
        await _users.SaveChangesAsync();

        _logger.LogInformation("Conta externa criada: {Username} pelo Admin: {Admin}",
            req.Username, User.Identity?.Name);

        return Ok(new { user.Id, user.Username, message = "Conta externa criada com sucesso" });
    }
}