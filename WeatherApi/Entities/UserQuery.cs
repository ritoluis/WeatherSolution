namespace WeatherApi.Entities;

public class UserQuery
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public DateTime QueryDate { get; set; } = DateTime.UtcNow;
    public string Endpoint { get; set; } = string.Empty;
    public DateOnly TargetDate { get; set; }
}