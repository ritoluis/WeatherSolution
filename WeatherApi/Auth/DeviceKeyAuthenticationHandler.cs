using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using WeatherApi.Services;

namespace WeatherApi.Auth;

public class DeviceKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string HeaderName { get; set; } = "X-Device-Key";
}

public class DeviceKeyAuthenticationHandler : AuthenticationHandler<DeviceKeyAuthenticationOptions>
{
    private readonly IApiKeyService _apiKeys;

    public DeviceKeyAuthenticationHandler(
        IOptionsMonitor<DeviceKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IApiKeyService apiKeys)
        : base(options, logger, encoder)
    {
        _apiKeys = apiKeys;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Options.HeaderName, out var providedKey))
            return AuthenticateResult.NoResult();

        var apiKey = providedKey.ToString();
        if (string.IsNullOrWhiteSpace(apiKey))
            return AuthenticateResult.NoResult();

        var device = await _apiKeys.AuthenticateAsync(apiKey);
        if (device == null)
        {
            Logger.LogWarning("API Key inválida recebida de {Ip}",
                Context.Connection.RemoteIpAddress);
            return AuthenticateResult.Fail("API Key inválida.");
        }

        if (!device.IsActive)
        {
            Logger.LogWarning("Dispositivo {DeviceId} ({Name}) está inativo.",
                device.Id, device.Name);
            return AuthenticateResult.Fail("Dispositivo inativo.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, device.Id.ToString()),
            new Claim(ClaimTypes.Name, device.Name),
            new Claim("device_id", device.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}