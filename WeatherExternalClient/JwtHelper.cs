using System.Text;
using System.Text.Json;

namespace WeatherExternalClient;

public static class JwtHelper
{
    public static DateTime? GetExpiry(string token)
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
            if (!doc.RootElement.TryGetProperty("exp", out var expElement))
                return null;

            return DateTimeOffset.FromUnixTimeSeconds(expElement.GetInt64()).UtcDateTime;
        }
        catch
        {
            return null;
        }
    }

    public static Dictionary<string, object>? GetClaims(string token)
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
            var result = new Dictionary<string, object>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = prop.Value.ToString();

            return result;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsExpiringSoon(string token, TimeSpan margin)
    {
        var expiry = GetExpiry(token);
        if (expiry == null) return true;
        return DateTime.UtcNow >= expiry.Value - margin;
    }
}