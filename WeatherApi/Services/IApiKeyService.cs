using WeatherApi.Entities;

namespace WeatherApi.Services;

public interface IApiKeyService
{
    string GenerateApiKey();
    string HashApiKey(string apiKey);
    bool VerifyApiKey(string providedKey, string storedHash);
    Task<Device?> AuthenticateAsync(string providedKey);
}