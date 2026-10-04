using System.Net.Http.Headers;
using System.Text.Json;
using WeatherWebApp.Models;

namespace WeatherWebApp.Services;


public class ApiClient : IApiClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new() { PropertyNameCaseInsensitive = true };

    public ApiClient(HttpClient http) => _http = http;

    private void SetToken(string token) =>
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public async Task<string?> LoginInternalAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login/internal", new { username, password });
        if (!resp.IsSuccessStatusCode) return null;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString();
    }

    public async Task<string?> LoginExternalAsync(string username, string password)
    {
        var resp = await _http.PostAsJsonAsync("api/auth/login/external", new { username, password });
        if (!resp.IsSuccessStatusCode) return null;
        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("token").GetString();
    }

    public async Task<TemperatureViewModel?> GetYesterdayAsync(string token)
    {
        SetToken(token);
        var resp = await _http.GetAsync("api/public/temperature/yesterday");
        if (!resp.IsSuccessStatusCode) return null;

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return new TemperatureViewModel
        {
            Date = DateOnly.Parse(doc.RootElement.GetProperty("date").GetString()!),
            AverageTemperature = doc.RootElement.GetProperty("averageTemperature").GetDecimal()
        };
    }

    public async Task<TemperatureViewModel?> GetByDateAsync(string token, DateOnly date)
    {
        SetToken(token);
        var resp = await _http.GetAsync($"api/internal/temperature/by-date/{date:yyyy-MM-dd}");
        if (!resp.IsSuccessStatusCode) return null;

        var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        return new TemperatureViewModel
        {
            Date = DateOnly.Parse(doc.RootElement.GetProperty("date").GetString()!),
            AverageTemperature = doc.RootElement.GetProperty("averageTemperature").GetDecimal()
        };
    }

    public async Task<List<QueryLogViewModel>?> GetAdminQueriesAsync(string token)
    {
        SetToken(token);
        var resp = await _http.GetAsync("api/admin/queries");
        if (!resp.IsSuccessStatusCode) return null;
        return JsonSerializer.Deserialize<List<QueryLogViewModel>>(
            await resp.Content.ReadAsStringAsync(), _json);
    }

    public async Task<List<UserListViewModel>?> GetAllUsersAsync(string token)
    {
        SetToken(token);
        var resp = await _http.GetAsync("api/admin/users");
        if (!resp.IsSuccessStatusCode) return null;
        return JsonSerializer.Deserialize<List<UserListViewModel>>(
            await resp.Content.ReadAsStringAsync(), _json);
    }

    public async Task<(bool Success, string Message)> CreateUserAsync(string token, CreateUserViewModel model)
    {
        SetToken(token);
        var endpoint = model.UserType == "external"
            ? "api/auth/create-external"
            : "api/admin/users/internal";

        var resp = await _http.PostAsJsonAsync(endpoint, new
        {
            username = model.Username,
            password = model.Password
        });

        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
            return (false, body);

        return (true, "Utilizador criado com sucesso");
    }
}