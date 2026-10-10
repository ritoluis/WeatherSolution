namespace WeatherWebApp.Extensions;

public static class DateTimeExtensions
{
    /// <summary>
    /// Converte um DateTime (assumido UTC) para hora local do servidor.
    /// </summary>
    public static DateTime ToLocal(this DateTime value)
    {
        return DateTime.SpecifyKind(value, DateTimeKind.Utc).ToLocalTime();
    }

    /// <summary>
    /// Versão nullable.
    /// </summary>
    public static DateTime? ToLocal(this DateTime? value)
    {
        return value?.ToLocal();
    }

    /// <summary>
    /// Formata para string legível em hora local.
    /// </summary>
    public static string ToLocalString(this DateTime value, string format = "yyyy-MM-dd HH:mm")
    {
        return value.ToLocal().ToString(format);
    }

    /// <summary>
    /// Versão nullable.
    /// </summary>
    public static string ToLocalString(this DateTime? value, string format = "yyyy-MM-dd HH:mm")
    {
        return value.HasValue ? value.Value.ToLocalString(format) : "-";
    }
}