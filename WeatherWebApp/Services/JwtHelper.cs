using System.Text;
using System.Text.Json;

namespace WeatherWebApp.Services;

public static class JwtHelper
{
    public static string? GetClaim(string token, string claimName)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            switch (payload.Length % 4)
            {
                case 2: payload += "=="; break;
                case 3: payload += "="; break;
            }

            var bytes = Convert.FromBase64String(payload);
            var json = Encoding.UTF8.GetString(bytes);

            using var doc = JsonDocument.Parse(json);
            // A claim de role do ASP.NET Core é "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
            // mas o JWT costuma abreviá-la para "role".
            // Procuramos ambas as formas.
            var keys = new[]
            {
                claimName,
                $"http://schemas.microsoft.com/ws/2008/06/identity/claims/{claimName}"
            };

            foreach (var key in keys)
            {
                if (doc.RootElement.TryGetProperty(key, out var el))
                    return el.GetString();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }
}