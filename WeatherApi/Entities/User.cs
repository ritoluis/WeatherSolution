namespace WeatherApi.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "User";
    public string UserType { get; set; } = "Internal";   // ← NOVO: "Internal" ou "External"
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public ICollection<UserQuery> Queries { get; set; } = new List<UserQuery>();
}