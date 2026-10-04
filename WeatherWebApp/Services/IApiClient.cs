using WeatherWebApp.Models;

namespace WeatherWebApp.Services;

public interface IApiClient
{
    Task<string?> LoginInternalAsync(string username, string password);
    Task<string?> LoginExternalAsync(string username, string password);
    Task<TemperatureViewModel?> GetByDateAsync(string token, DateOnly date);
    Task<TemperatureViewModel?> GetYesterdayAsync(string token);
    Task<List<QueryLogViewModel>?> GetAdminQueriesAsync(string token);
    Task<List<UserListViewModel>?> GetAllUsersAsync(string token);
    Task<(bool Success, string Message)> CreateUserAsync(string token, CreateUserViewModel model);
}