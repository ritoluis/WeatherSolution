namespace WeatherApi.DTOs;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string Username, string Role);
public record CreateUserRequest(string Username, string Password);