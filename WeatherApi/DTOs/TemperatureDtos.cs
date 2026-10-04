namespace WeatherApi.DTOs;

public record TemperatureResponse(DateOnly Date, decimal? AverageTemperature);