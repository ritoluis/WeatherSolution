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
        var resp = await _http.GetAsync("api/internal/temperature/yesterday");
        var body = await resp.Content.ReadAsStringAsync();

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Extrai a mensagem do corpo de erro
            try
            {
                var doc = JsonDocument.Parse(body);
                return new TemperatureViewModel
                {
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    Message = doc.RootElement.GetProperty("message").GetString()
                              ?? "Sem leitura registada."
                };
            }
            catch
            {
                return new TemperatureViewModel
                {
                    Date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)),
                    Message = "Sem leitura registada."
                };
            }
        }

        if (!resp.IsSuccessStatusCode) return null;

        var okDoc = JsonDocument.Parse(body);
        return new TemperatureViewModel
        {
            Date = DateOnly.Parse(okDoc.RootElement.GetProperty("date").GetString()!),
            AverageTemperature = okDoc.RootElement.GetProperty("averageTemperature").GetDecimal()
        };
    }

    public async Task<TemperatureViewModel?> GetByDateAsync(string token, DateOnly date)
    {
        SetToken(token);
        var resp = await _http.GetAsync($"api/internal/temperature/by-date/{date:yyyy-MM-dd}");
        var body = await resp.Content.ReadAsStringAsync();

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            try
            {
                var doc = JsonDocument.Parse(body);
                return new TemperatureViewModel
                {
                    Date = date,
                    Message = doc.RootElement.GetProperty("message").GetString()
                              ?? "Sem leitura registada."
                };
            }
            catch
            {
                return new TemperatureViewModel
                {
                    Date = date,
                    Message = "Sem leitura registada."
                };
            }
        }

        if (!resp.IsSuccessStatusCode) return null;

        var okDoc = JsonDocument.Parse(body);
        return new TemperatureViewModel
        {
            Date = DateOnly.Parse(okDoc.RootElement.GetProperty("date").GetString()!),
            AverageTemperature = okDoc.RootElement.GetProperty("averageTemperature").GetDecimal()
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

    // Na classe ApiClient, acrescenta os métodos:

    public async Task<List<DeviceListViewModel>?> GetDevicesAsync(string token)
    {
        SetToken(token);
        var resp = await _http.GetAsync("api/admin/devices");
        if (!resp.IsSuccessStatusCode) return null;
        return JsonSerializer.Deserialize<List<DeviceListViewModel>>(
            await resp.Content.ReadAsStringAsync(), _json);
    }

    public async Task<DeviceDetailsViewModel?> GetDeviceDetailsAsync(string token, int id, int limit = 30)
    {
        SetToken(token);

        var deviceResp = await _http.GetAsync($"api/admin/devices/{id}");
        if (!deviceResp.IsSuccessStatusCode) return null;

        var device = JsonSerializer.Deserialize<DeviceListViewModel>(
            await deviceResp.Content.ReadAsStringAsync(), _json);
        if (device == null) return null;

        var readingsResp = await _http.GetAsync($"api/admin/devices/{id}/readings?limit={limit}");
        var readings = readingsResp.IsSuccessStatusCode
            ? JsonSerializer.Deserialize<DeviceReadingsResponse>(
                await readingsResp.Content.ReadAsStringAsync(), _json)
            : null;

        return new DeviceDetailsViewModel
        {
            Id = device.Id,
            Name = device.Name,
            Location = device.Location,
            IsActive = device.IsActive,
            CreatedAt = device.CreatedAt,
            LastSeenAt = device.LastSeenAt,
            Readings = readings?.Readings ?? new()
        };
    }

    public async Task<CreateDeviceResultViewModel?> CreateDeviceAsync(string token, CreateDeviceViewModel model)
    {
        SetToken(token);
        var resp = await _http.PostAsJsonAsync("api/admin/devices", new
        {
            name = model.Name,
            location = model.Location
        });
        if (!resp.IsSuccessStatusCode) return null;

        return JsonSerializer.Deserialize<CreateDeviceResultViewModel>(
            await resp.Content.ReadAsStringAsync(), _json);
    }

    public async Task<bool> ActivateDeviceAsync(string token, int id)
    {
        SetToken(token);
        var resp = await _http.PutAsync($"api/admin/devices/{id}/activate", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeactivateDeviceAsync(string token, int id)
    {
        SetToken(token);
        var resp = await _http.PutAsync($"api/admin/devices/{id}/deactivate", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteDeviceAsync(string token, int id)
    {
        SetToken(token);
        var resp = await _http.DeleteAsync($"api/admin/devices/{id}");
        return resp.IsSuccessStatusCode;
    }

    public async Task<RequestReadingResultViewModel> RequestReadingAsync(string token, int id)
    {
        SetToken(token);
        var resp = await _http.PostAsync($"api/admin/devices/{id}/request-reading", null);

        if (resp.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
        {
            return new RequestReadingResultViewModel
            {
                Success = false,
                Message = "O dispositivo não respondeu em 10 segundos. Verifique se está ligado ao broker MQTT.",
                DeviceId = id
            };
        }

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            return new RequestReadingResultViewModel
            {
                Success = false,
                Message = $"Erro {(int)resp.StatusCode}: {body}",
                DeviceId = id
            };
        }

        var result = JsonSerializer.Deserialize<RequestReadingResponse>(
            await resp.Content.ReadAsStringAsync(), _json);

        return new RequestReadingResultViewModel
        {
            Success = true,
            Message = "Leitura obtida com sucesso.",
            DeviceId = result?.DeviceId ?? id,
            DeviceName = result?.DeviceName ?? string.Empty,
            Temperature = result?.Temperature,
            ReadAt = result?.ReadAt
        };
    }


}
// Classes internas para desserializar respostas da API
internal class DeviceReadingsResponse
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public List<DeviceReadingViewModel> Readings { get; set; } = new();
}

internal class RequestReadingResponse
{
    public int DeviceId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public decimal Temperature { get; set; }
    public DateTime ReadAt { get; set; }
}