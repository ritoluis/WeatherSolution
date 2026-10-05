using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Http.Headers;

namespace WeatherExternalClient;

public interface IWeatherClient
{
    Task<(HttpStatusCode Status, string Body)> GetYesterdayAsync();
    Task<(HttpStatusCode Status, string Body)> CallInternalByDateAsync(DateOnly date);
    Task<(HttpStatusCode Status, string Body)> CallAdminQueriesAsync();
}

public class WeatherClient : IWeatherClient
{
    private readonly HttpClient _http;
    private readonly ITokenService _tokens;
    private readonly ILogger<WeatherClient> _logger;

    public WeatherClient(HttpClient http, ITokenService tokens, ILogger<WeatherClient> logger)
    {
        _http = http;
        _tokens = tokens;
        _logger = logger;
    }

    private async Task<(HttpStatusCode, string)> SendAsync(
        HttpMethod method, string path, bool retryOn401 = true)
    {
        var token = await _tokens.GetValidTokenAsync();
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                $"Falha de rede ao contactar {_http.BaseAddress}{path}. " +
                $"A API está a correr? Detalhe: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            throw new ApiException(
                $"Timeout ao contactar {_http.BaseAddress}{path}. Detalhe: {ex.Message}", ex);
        }

        var body = await resp.Content.ReadAsStringAsync();

        // Se o token foi rejeitado, renova e tenta uma vez
        if (resp.StatusCode == HttpStatusCode.Unauthorized && retryOn401)
        {
            _logger.LogWarning("Recebido 401. A renovar token e a tentar de novo...");
            _tokens.ForceRenew();

            token = await _tokens.GetValidTokenAsync();
            request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            try
            {
                resp = await _http.SendAsync(request);
            }
            catch (HttpRequestException ex)
            {
                throw new ApiException(
                    $"Falha de rede ao repetir o pedido a {path}. Detalhe: {ex.Message}", ex);
            }
            body = await resp.Content.ReadAsStringAsync();
        }

        return (resp.StatusCode, body);
    }

    public Task<(HttpStatusCode, string)> GetYesterdayAsync() =>
        SendAsync(HttpMethod.Get, "api/public/temperature/yesterday");

    public Task<(HttpStatusCode, string)> CallInternalByDateAsync(DateOnly date) =>
        SendAsync(HttpMethod.Get, $"api/internal/temperature/by-date/{date:yyyy-MM-dd}", retryOn401: false);

    public Task<(HttpStatusCode, string)> CallAdminQueriesAsync() =>
        SendAsync(HttpMethod.Get, "api/admin/queries", retryOn401: false);
}