using Microsoft.Extensions.Caching.Memory;

namespace WeatherApi.Services;

public class LoginAttemptService : ILoginAttemptService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<LoginAttemptService> _logger;

    private const int MaxAttempts = 3;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public LoginAttemptService(IMemoryCache cache, ILogger<LoginAttemptService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public bool IsLockedOut(string username)
    {
        return _cache.TryGetValue(LockoutKey(username), out _);
    }

    public void RecordFailedAttempt(string username)
    {
        var key = AttemptsKey(username);
        var attempts = _cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = LockoutDuration;
            return 0;
        });

        attempts++;
        _cache.Set(key, attempts, LockoutDuration);

        _logger.LogWarning("Tentativa falhada {Attempts}/{Max} para username: {Username}",
            attempts, MaxAttempts, username);

        if (attempts >= MaxAttempts)
        {
            _cache.Set(LockoutKey(username), true, LockoutDuration);
            _logger.LogWarning("Conta bloqueada temporariamente: {Username} por {Minutes} minutos",
                username, LockoutDuration.TotalMinutes);
        }
    }

    public void ResetAttempts(string username)
    {
        _cache.Remove(AttemptsKey(username));
        _cache.Remove(LockoutKey(username));
    }

    public int GetRemainingAttempts(string username)
    {
        var attempts = _cache.Get<int?>(AttemptsKey(username)) ?? 0;
        return Math.Max(0, MaxAttempts - attempts);
    }

    private static string AttemptsKey(string username) => $"attempts_{username.ToLowerInvariant()}";
    private static string LockoutKey(string username) => $"lockout_{username.ToLowerInvariant()}";
}