namespace WeatherApi.DTOs;

public record DeviceReadingRequest(DateOnly Date, decimal AverageTemperature);

public record DeviceConfigResponse(
    int DeviceId,
    string Name,
    bool IsActive,
    bool KeyRotationPending,
    string? NewApiKey);

public record DeviceInfoResponse(
    int Id,
    string Name,
    string? Location,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    DateOnly? LastReadingDate,
    decimal? LastReadingTemperature);
public record CreateDeviceRequest(string Name, string? Location);