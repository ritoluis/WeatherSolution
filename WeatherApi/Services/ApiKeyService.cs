using Microsoft.EntityFrameworkCore;
using WeatherApi.Data;
using WeatherApi.Entities;

namespace WeatherApi.Services;

public class ApiKeyService : IApiKeyService
{
    private readonly WeatherDbContext _context;
    private readonly IConfiguration _config;
    private readonly ILogger<ApiKeyService> _logger;

    public ApiKeyService(WeatherDbContext context, IConfiguration config,
        ILogger<ApiKeyService> logger)
    {
        _context = context;
        _config = config;
        _logger = logger;
    }

    public string GenerateApiKey()
    {
        var prefix = _config["DeviceAuth:KeyPrefix"] ?? "dev_";
        var length = int.Parse(_config["DeviceAuth:KeyLength"] ?? "32");

        // Gera bytes aleatórios criptograficamente seguros
        var bytes = new byte[length];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);

        // Converte para Base64 URL-safe e remove caracteres problemáticos
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "");

        return prefix + token;
    }

    public string HashApiKey(string apiKey)
    {
        // BCrypt com fator 12 (mais lento que o default 10, mais seguro)
        return BCrypt.Net.BCrypt.HashPassword(apiKey, workFactor: 12);
    }

    public bool VerifyApiKey(string providedKey, string storedHash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(providedKey, storedHash);
        }
        catch
        {
            return false;
        }
    }

    public async Task<Device?> AuthenticateAsync(string providedKey)
    {
        if (string.IsNullOrWhiteSpace(providedKey))
            return null;

        // Como o BCrypt não permite procurar diretamente por hash,
        // temos de iterar sobre os dispositivos. Para otimizar, fazemos
        // uma verificação rápida por prefixo e só depois BCrypt.
        var devices = await _context.Devices.ToListAsync();

        foreach (var device in devices)
        {
            // Verifica contra a chave atual
            if (VerifyApiKey(providedKey, device.ApiKeyHash))
                return device;

            // Verifica também contra a chave pendente (durante rotação)
            if (device.KeyRotationPending && device.PendingApiKeyHash != null &&
                VerifyApiKey(providedKey, device.PendingApiKeyHash))
            {
                _logger.LogInformation(
                    "Dispositivo {DeviceId} autenticado com chave pendente.", device.Id);
                return device;
            }
        }

        return null;
    }
}