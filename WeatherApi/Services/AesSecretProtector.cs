using System.Security.Cryptography;
using System.Text;

namespace WeatherApi.Services;

public class AesSecretProtector : ISecretProtector
{
    private readonly byte[] _key;

    public AesSecretProtector(IConfiguration config)
    {
        var secret = config["Jwt:Key"]!;
        // Deriva uma chave de 32 bytes a partir da chave JWT
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public (byte[] cipher, byte[] iv) Protect(string plainText)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plainBytes = Encoding.UTF8.GetBytes(plainText);
        var cipher = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

        return (cipher, aes.IV);
    }

    public string? Unprotect(byte[] cipher, byte[] iv)
    {
        try
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
            var plainBytes = decryptor.TransformFinalBlock(cipher, 0, cipher.Length);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            return null;
        }
    }
}