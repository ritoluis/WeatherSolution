using WeatherApi.Entities;

namespace WeatherApi.Services;

public interface ITokenService
{
    string GenerateToken(User user, string clientType);
}
