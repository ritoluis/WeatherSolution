using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace WeatherExternalClient;

public interface ITokenService
{
    Task<string> GetValidTokenAsync();
    string? CurrentToken { get; }
    DateTime? CurrentExpiry { get; }
    void ForceRenew();
}

public class TokenService : ITokenService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<TokenService> _logger;

    private string? _token;
    private DateTime? _expiry;
    private static readonly TimeSpan RenewalMargin = TimeSpan.FromMinutes(5);

    public TokenService(HttpClient http, IConfiguration config, ILogger<TokenService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public string? CurrentToken => _token;
    public DateTime? CurrentExpiry => _expiry;

    public void ForceRenew()
    {
        _token = null;
        _expiry = null;
        _logger.LogInformation("Token forçado a renovar.");
    }

    public async Task<string> GetValidTokenAsync()
    {
        if (_token != null && !JwtHelper.IsExpiringSoon(_token, RenewalMargin))
        {
            _logger.LogDebug("Reutilizado token existente.");
            return _token;
        }

        _logger.LogInformation("Token ausente ou prestes a expirar — a renovar...");
        return await RequestNewTokenAsync();
    }

    private async Task<string> RequestNewTokenAsync()
    {
        var username = _config["Credentials:Username"]!;
        var password = _config["Credentials:Password"]!;
        var baseUrl = _http.BaseAddress?.ToString() ?? "(não configurado)";

        HttpResponseMessage resp;
        try
        {
            resp = await _http.PostAsJsonAsync("api/auth/login/external", new
            {
                username,
                password
            });
        }
        catch (HttpRequestException ex)
        {
            // Erro de rede: API não está a correr, DNS errado, porta errada
            throw new ApiException(
                $"Não foi possível contactar a API em {baseUrl}. " +
                $"Confirme que a API está a correr. Detalhe: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            // Timeout (HttpClient default = 100s)
            throw new ApiException(
                $"O pedido à API em {baseUrl} expirou. Detalhe: {ex.Message}", ex);
        }

        var body = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode)
        {
            throw new ApiException(
                $"Login falhou: {(int)resp.StatusCode} {resp.ReasonPhrase}. Corpo: {body}");
        }

        try
        {
            var doc = JsonDocument.Parse(body);
            _token = doc.RootElement.GetProperty("token").GetString()
                ?? throw new ApiException("A resposta de login não contém 'token'.");

            _expiry = JwtHelper.GetExpiry(_token);
            _logger.LogInformation("Novo token obtido. Expira em: {Expiry} UTC", _expiry);
            return _token;
        }
        catch (JsonException ex)
        {
            throw new ApiException(
                $"Resposta de login inválida (não é JSON esperado). Corpo: {body}", ex);
        }
        catch (KeyNotFoundException ex)
        {
            throw new ApiException(
                $"Resposta de login sem campo 'token'. Corpo: {body}", ex);
        }
    }
}