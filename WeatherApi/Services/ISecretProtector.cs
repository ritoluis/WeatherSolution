namespace WeatherApi.Services;

public interface ISecretProtector
{
    (byte[] cipher, byte[] iv) Protect(string plainText);
    string? Unprotect(byte[] cipher, byte[] iv);
}