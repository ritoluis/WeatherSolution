using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WeatherApi.DTOs;
using WeatherApi.Entities;
using WeatherApi.Repositories;
using WeatherApi.Services;

namespace WeatherApi.Controllers;

[ApiController]
[Route("api/device")]
[Authorize(AuthenticationSchemes = "DeviceKey")]
public class DeviceController : ControllerBase
{
    private readonly IDeviceRepository _devices;
    private readonly ITemperatureService _temperature;
    private readonly IApiKeyService _apiKeys;
    private readonly ILogger<DeviceController> _logger;
    private readonly ISecretProtector _protector;

    public DeviceController(
        IDeviceRepository devices,
        ITemperatureService temperature,
        IApiKeyService apiKeys,
        ILogger<DeviceController> logger,
        ISecretProtector protector)
    {
        _devices = devices;
        _temperature = temperature;
        _apiKeys = apiKeys;
        _logger = logger;
        _protector = protector;
    }

    private int CurrentDeviceId =>
        int.Parse(User.FindFirstValue("device_id")!);

    // ============ Envio de leitura diária ============
    [HttpPost("reading")]
    public async Task<IActionResult> SubmitReading([FromBody] DeviceReadingRequest req)
    {
        var deviceId = CurrentDeviceId;

        // Validação básica
        if (req.AverageTemperature < -50 || req.AverageTemperature > 60)
        {
            _logger.LogWarning("Dispositivo {DeviceId} enviou valor fora do intervalo: {Temp}",
                deviceId, req.AverageTemperature);
            return BadRequest(new { message = "Temperatura fora do intervalo aceitável." });
        }

        // Impede leituras para o futuro
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (req.Date > today)
            return BadRequest(new { message = "Data da leitura não pode ser futura." });

        // Verifica se já existe uma leitura para este dia
        var existing = await _devices.GetReadingAsync(deviceId, req.Date);
        if (existing != null)
        {
            // Idempotência: substitui o valor
            existing.AverageTemperature = req.AverageTemperature;
            existing.ReceivedAt = DateTime.UtcNow;
            await _devices.UpdateReadingAsync(existing);
            _logger.LogInformation(
                "Dispositivo {DeviceId} reenviou leitura para {Date}. Substituído por {Temp}",
                deviceId, req.Date, req.AverageTemperature);
        }
        else
        {
            await _devices.AddReadingAsync(new DeviceReading
            {
                DeviceId = deviceId,
                ReadingDate = req.Date,
                AverageTemperature = req.AverageTemperature,
                ReceivedAt = DateTime.UtcNow
            });
            _logger.LogInformation(
                "Dispositivo {DeviceId} enviou leitura para {Date}: {Temp}",
                deviceId, req.Date, req.AverageTemperature);
        }

        // Atualiza LastSeenAt no dispositivo
        var device = await GetCurrentDeviceEntityAsync();
        if (device != null)
        {
            device.LastSeenAt = DateTime.UtcNow;
        }

        await _devices.SaveChangesAsync();

        // Recalcula a média agregada do dia
        await _temperature.RecalculateDailyAverageAsync(req.Date);

        return Ok(new { message = "Leitura registada com sucesso." });
    }

    // ============ Consulta de configuração (rotação de chave) ============
    [HttpGet("config")]
    public async Task<IActionResult> GetConfig()
    {
        var deviceId = CurrentDeviceId;
        var device = await GetCurrentDeviceEntityAsync();

        if (device == null)
            return NotFound();

        // Se há rotação pendente, devolve a nova chave em claro.
        // Esta é a única forma de o dispositivo a obter.
        string? newKey = null;
        if (device.KeyRotationPending)
        {
            // A chave pendente é armazenada como hash, mas também
            // precisamos da versão em claro para enviar ao dispositivo.
            // Solução: guardar temporariamente a versão em claro numa
            // tabela auxiliar ou num campo protegido. Ver nota abaixo.
            newKey = await GetPendingPlainKeyAsync(device);
        }

        return Ok(new DeviceConfigResponse(
            DeviceId: device.Id,
            Name: device.Name,
            IsActive: device.IsActive,
            KeyRotationPending: device.KeyRotationPending,
            NewApiKey: newKey
        ));
    }

    // ============ Confirmação de adoção de nova chave ============
    [HttpPost("confirm-key")]
    public async Task<IActionResult> ConfirmKeyRotation()
    {
        var device = await GetCurrentDeviceEntityAsync();
        if (device == null) return NotFound();

        if (!device.KeyRotationPending)
            return Ok(new { message = "Nenhuma rotação pendente." });

        // O dispositivo autenticou-se com a nova chave (que já está no hash pendente)
        // Move a chave pendente para a chave atual
        device.ApiKeyHash = device.PendingApiKeyHash!;
        device.PendingApiKeyHash = null;
        device.KeyRotationPending = false;

        await _devices.SaveChangesAsync();

        _logger.LogInformation("Dispositivo {DeviceId} confirmou adoção de nova chave.", device.Id);
        return Ok(new { message = "Rotação de chave confirmada." });
    }

    private async Task<Device?> GetCurrentDeviceEntityAsync()
    {
        var deviceId = CurrentDeviceId;
        return await _devices.GetByIdAsync(deviceId);
    }

    // MÉTODO A IMPLEMENTAR: ver secção "Como devolver a nova chave ao dispositivo" abaixo
    private Task<string?> GetPendingPlainKeyAsync(Device device)
    {
        if (device.PendingApiKeyEncrypted == null || device.PendingApiKeyIv == null)
            return Task.FromResult<string?>(null);

        var plain = _protector.Unprotect(
            device.PendingApiKeyEncrypted,
            device.PendingApiKeyIv);

        return Task.FromResult(plain);
    }
}