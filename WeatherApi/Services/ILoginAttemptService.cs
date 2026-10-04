

namespace WeatherApi.Services;

public interface ILoginAttemptService
{
    bool IsLockedOut(string username);
    void RecordFailedAttempt(string username);
    void ResetAttempts(string username);
    int GetRemainingAttempts(string username);
}

