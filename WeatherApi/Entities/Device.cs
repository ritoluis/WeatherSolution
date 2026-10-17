namespace WeatherApi.Entities;

public class Device
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public string ApiKeyHash { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAt { get; set; }

    // Rotação de chave
    public bool KeyRotationPending { get; set; } = false;
    public string? PendingApiKeyHash { get; set; }

    public byte[]? PendingApiKeyEncrypted { get; set; }
    public byte[]? PendingApiKeyIv { get; set; }

    public ICollection<DeviceReading> Readings { get; set; } = new List<DeviceReading>();
}