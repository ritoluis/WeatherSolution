using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherApi.DTOs;
using WeatherApi.Entities;
using WeatherApi.Repositories;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IUserRepository _users;
    private readonly IDeviceRepository _devices;
    private readonly IApiKeyService _apiKeys;
    private readonly ISecretProtector _protector;
    private readonly ILogger<AdminController> _logger;
    private readonly IMqttService _mqtt;

    public AdminController(
        IUserRepository users,
        IDeviceRepository devices,
        IApiKeyService apiKeys,
        ISecretProtector protector,
        ILogger<AdminController> logger,
        IMqttService mqtt)
    {
        _users = users;
        _devices = devices;
        _apiKeys = apiKeys;
        _protector = protector;
        _logger = logger;
        _mqtt = mqtt;
    }

    // ============================================================
    //  AUDITORIA E UTILIZADORES
    // ============================================================

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
            Role = "User",
            UserType = "Internal",       // ← NOVO
            CreatedAt = DateTime.UtcNow
        };

        await _users.AddAsync(user);
        await _users.SaveChangesAsync();

        _logger.LogInformation("Utilizador interno criado: {Username} pelo Admin: {Admin}",
            req.Username, User.Identity?.Name);

        return Ok(new { user.Id, user.Username, message = "Utilizador interno criado" });
    }

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

    // ============================================================
    //  DISPOSITIVOS
    // ============================================================

    // -------- Listar todos os dispositivos --------
    [HttpGet("devices")]
    public async Task<IActionResult> GetAllDevices()
    {
        var devices = await _devices.GetAllAsync();

        var result = new List<DeviceInfoResponse>();
        foreach (var d in devices)
        {
            var lastReading = await _devices.GetLatestReadingAsync(d.Id);

            result.Add(new DeviceInfoResponse(
                Id: d.Id,
                Name: d.Name,
                Location: d.Location,
                IsActive: d.IsActive,
                CreatedAt: d.CreatedAt,
                LastSeenAt: d.LastSeenAt,
                LastReadingDate: lastReading?.ReadingDate,
                LastReadingTemperature: lastReading?.AverageTemperature
            ));
        }

        return Ok(result);
    }

    // -------- Detalhes de um dispositivo --------
    [HttpGet("devices/{id:int}")]
    public async Task<IActionResult> GetDevice(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();

        var lastReading = await _devices.GetLatestReadingAsync(id);

        return Ok(new DeviceInfoResponse(
            Id: device.Id,
            Name: device.Name,
            Location: device.Location,
            IsActive: device.IsActive,
            CreatedAt: device.CreatedAt,
            LastSeenAt: device.LastSeenAt,
            LastReadingDate: lastReading?.ReadingDate,
            LastReadingTemperature: lastReading?.AverageTemperature
        ));
    }

    // -------- Criar dispositivo --------
    [HttpPost("devices")]
    public async Task<IActionResult> CreateDevice([FromBody] CreateDeviceRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Nome do dispositivo é obrigatório." });

        // Gera a API Key
        var apiKey = _apiKeys.GenerateApiKey();

        var device = new Device
        {
            Name = req.Name,
            Location = req.Location,
            ApiKeyHash = _apiKeys.HashApiKey(apiKey),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _devices.AddAsync(device);
        await _devices.SaveChangesAsync();

        _logger.LogInformation(
            "Dispositivo criado: {Name} (Id={Id}) pelo Admin: {Admin}",
            device.Name, device.Id, User.Identity?.Name);

        // A API Key em claro é devolvida UMA ÚNICA VEZ.
        // Depois disto, nem o admin a consegue ver — só regenerar.
        return Ok(new
        {
            device.Id,
            device.Name,
            device.Location,
            device.IsActive,
            device.CreatedAt,
            apiKey,
            message = "Dispositivo criado. Guarde a API Key — não será mostrada novamente."
        });
    }

    // -------- Ativar dispositivo --------
    [HttpPut("devices/{id:int}/activate")]
    public async Task<IActionResult> ActivateDevice(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();

        device.IsActive = true;
        await _devices.SaveChangesAsync();

        _logger.LogInformation("Dispositivo {Id} ativado pelo Admin: {Admin}",
            id, User.Identity?.Name);

        return Ok(new { message = "Dispositivo ativado." });
    }

    // -------- Desativar dispositivo --------
    [HttpPut("devices/{id:int}/deactivate")]
    public async Task<IActionResult> DeactivateDevice(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();

        device.IsActive = false;
        await _devices.SaveChangesAsync();

        _logger.LogInformation("Dispositivo {Id} desativado pelo Admin: {Admin}",
            id, User.Identity?.Name);

        return Ok(new { message = "Dispositivo desativado." });
    }

    // -------- Eliminar dispositivo --------
    [HttpDelete("devices/{id:int}")]
    public async Task<IActionResult> DeleteDevice(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();

        await _devices.DeleteAsync(device);
        await _devices.SaveChangesAsync();

        _logger.LogWarning("Dispositivo {Id} ({Name}) eliminado pelo Admin: {Admin}",
            id, device.Name, User.Identity?.Name);

        return Ok(new { message = "Dispositivo eliminado." });
    }

    // -------- Regenerar API Key --------
    //  AQUI ENTRA O TRECHO QUE TE CONFUNDIU
    [HttpPost("devices/{id:int}/regenerate-key")]
    public async Task<IActionResult> RegenerateApiKey(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();

        // 1. Gera uma nova API Key em claro
        var newKey = _apiKeys.GenerateApiKey();

        // 2. Guarda o hash da nova chave (será a chave "pendente")
        device.PendingApiKeyHash = _apiKeys.HashApiKey(newKey);

        // 3. Cifra a chave em claro e guarda-a (para a poder enviar
        //    ao dispositivo quando este fizer polling a /api/device/config)
        var (cipher, iv) = _protector.Protect(newKey);
        device.PendingApiKeyEncrypted = cipher;
        device.PendingApiKeyIv = iv;

        // 4. Marca que há uma rotação pendente.
        //    O dispositivo continua a autenticar-se com a chave ANTIGA
        //    até confirmar a adoção da nova.
        device.KeyRotationPending = true;

        await _devices.SaveChangesAsync();

        _logger.LogInformation(
            "API Key regenerada para dispositivo {Id} ({Name}) pelo Admin: {Admin}. " +
            "Aguardando confirmação do dispositivo.",
            id, device.Name, User.Identity?.Name);

        // 5. A chave em claro é devolvida APENAS UMA VEZ ao admin,
        //    para o caso de ele querer injetá-la manualmente no dispositivo.
        //    Depois disso, só o dispositivo a recebe via /api/device/config.
        return Ok(new
        {
            newApiKey = newKey,
            message = "Nova API Key gerada. O dispositivo irá adotá-la no próximo " +
                      "ciclo de polling. Guarde-a agora — não será mostrada novamente."
        });
    }

    // -------- Histórico de leituras de um dispositivo --------
    [HttpGet("devices/{id:int}/readings")]
    public async Task<IActionResult> GetDeviceReadings(
        int id,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] int limit = 30)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();

        if (from == null)
            from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-limit));
        if (to == null)
            to = DateOnly.FromDateTime(DateTime.UtcNow);

        var readings = await _devices.GetReadingsForDeviceAsync(id, from.Value, to.Value);

        return Ok(new
        {
            deviceId = id,
            deviceName = device.Name,
            from,
            to,
            readings = readings.Select(r => new
            {
                r.ReadingDate,
                r.AverageTemperature,
                r.ReceivedAt
            })
        });
    }

    // ============================================================
    //  TEMPERATURA ATUAL (leitura real no momento da consulta)
    // ============================================================

    // -------- Média geral atual --------
    [HttpGet("temperature/current")]
    public async Task<IActionResult> GetCurrentAverageTemperature()
    {
        var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
        var reading = await _devices.GetAggregateReadingAsync(yesterday);

        return Ok(new
        {
            date = yesterday,
            averageTemperature = reading,
            message = reading == null
                ? "Sem leituras para o dia anterior."
                : "Média calculada com base nos dispositivos ativos."
        });
    }

    // -------- Solicitar leitura imediata de um dispositivo --------

    [HttpPost("devices/{id:int}/request-reading")]
    public async Task<IActionResult> RequestImmediateReading(int id)
    {
        var device = await _devices.GetByIdAsync(id);
        if (device == null) return NotFound();
        if (!device.IsActive)
            return BadRequest(new { message = "Dispositivo inativo." });

        var temperature = await _mqtt.RequestReadingAsync(id, TimeSpan.FromSeconds(10));

        if (temperature == null)
            return StatusCode(504, new
            {
                message = "O dispositivo não respondeu a tempo. Verifique se está ligado e a subscrever o tópico."
            });

        return Ok(new
        {
            deviceId = id,
            deviceName = device.Name,
            temperature = temperature.Value,
            readAt = DateTime.UtcNow
        });
    }
}