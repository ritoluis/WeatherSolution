using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace WeatherExternalClient;

public class Menu
{
    private readonly IWeatherClient _weather;
    private readonly ITokenService _tokens;
    private readonly IConfiguration _config;

    public Menu(IWeatherClient weather, ITokenService tokens, IConfiguration config)
    {
        _weather = weather;
        _tokens = tokens;
        _config = config;
    }

    public async Task RunAsync()
    {
        while (true)
        {
            PrintHeader();
            Console.WriteLine("  [1] Consultar temperatura de ontem");
            Console.WriteLine("  [2] Ver credenciais e token atual");
            Console.WriteLine("  [3] Forçar renovação do token");
            Console.WriteLine("  [4] Testar endpoint interno (deve dar 403)");
            Console.WriteLine("  [5] Testar endpoint admin (deve dar 403)");
            Console.WriteLine("  [0] Sair");
            Console.WriteLine();
            Console.Write("Opção: ");

            var option = Console.ReadLine();
            Console.WriteLine();

            try
            {
                switch (option)
                {
                    case "1": await ConsultYesterdayAsync(); break;
                    case "2": await ShowCredentialsAsync(); break;
                    case "3": ForceRenew(); break;
                    case "4": await TestInternalAsync(); break;
                    case "5": await TestAdminAsync(); break;
                    case "0": return;
                    default: Console.WriteLine("Opção inválida."); break;
                }
            }
            catch (ApiException ex)
            {
                PrintError($"Erro de comunicação com a API: {ex.Message}");
            }
            catch (Exception ex)
            {
                PrintError($"Erro inesperado: {ex.Message}");
            }

            Console.WriteLine();
            Console.WriteLine("Prima ENTER para continuar...");
            Console.ReadLine();
            Console.Clear();
        }
    }

    private void PrintHeader()
    {
        Console.Clear();
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Weather External Client — Simulador de Agente      ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    private static void PrintError(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  ✗ {message}");
        Console.ResetColor();
    }

    private static void PrintSuccess(string message)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"  ✓ {message}");
        Console.ResetColor();
    }

    private static void PrintWarning(string message)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"  {message}");
        Console.ResetColor();
    }

    private async Task ConsultYesterdayAsync()
    {
        Console.WriteLine("→ A consultar /api/public/temperature/yesterday ...");
        Console.WriteLine();

        var (status, body) = await _weather.GetYesterdayAsync();
        Console.WriteLine($"Estado HTTP: {(int)status} {status}");
        Console.WriteLine();

        if (status == HttpStatusCode.OK)
        {
            var doc = JsonDocument.Parse(body);
            var date = doc.RootElement.GetProperty("date").GetString();
            var temp = doc.RootElement.GetProperty("averageTemperature").GetDecimal();
            PrintSuccess($"Temperatura média de {date}: {temp} °C");
        }
        else
        {
            PrintWarning($"Resposta: {body}");
        }
    }

    private async Task ShowCredentialsAsync()
    {
        Console.WriteLine("Credenciais configuradas:");
        Console.WriteLine($"  Username: {_config["Credentials:Username"]}");
        Console.WriteLine($"  Password: {new string('*', _config["Credentials:Password"]?.Length ?? 0)}");
        Console.WriteLine($"  API URL:  {_config["ApiSettings:BaseUrl"]}");
        Console.WriteLine();

        var token = _tokens.CurrentToken;
        if (token == null)
        {
            Console.WriteLine("Sem token em cache. A obter...");
            Console.WriteLine();
            token = await _tokens.GetValidTokenAsync();
        }

        Console.WriteLine("Token atual:");
        Console.WriteLine($"  {token}");
        Console.WriteLine();

        Console.WriteLine("Claims do token:");
        var claims = JwtHelper.GetClaims(token);
        if (claims != null)
        {
            foreach (var kv in claims)
                Console.WriteLine($"  {kv.Key}: {kv.Value}");
        }

        Console.WriteLine();
        var expiry = _tokens.CurrentExpiry;
        if (expiry.HasValue)
        {
            var remaining = expiry.Value - DateTime.UtcNow;
            Console.WriteLine($"Expira em: {expiry:yyyy-MM-dd HH:mm:ss} UTC");
            Console.WriteLine($"Tempo restante: {remaining:mm\\:ss} minutos");
        }
    }

    private void ForceRenew()
    {
        _tokens.ForceRenew();
        PrintSuccess("Token marcado para renovação. Será obtido novo no próximo pedido.");
    }

    private async Task TestInternalAsync()
    {
        Console.WriteLine("→ A tentar /api/internal/temperature/by-date/{data} ...");
        Console.WriteLine("  (esperado: 403 Forbidden, pois este token é externo)");
        Console.WriteLine();

        var date = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2));
        var (status, body) = await _weather.CallInternalByDateAsync(date);

        Console.WriteLine($"Estado HTTP: {(int)status} {status}");
        Console.WriteLine($"Corpo: {body}");

        if (status == HttpStatusCode.Forbidden)
            PrintSuccess("Comportamento correto: token externo rejeitado no endpoint interno.");
        else if (status == HttpStatusCode.OK)
            PrintError("FALHA DE SEGURANÇA: token externo conseguiu aceder ao endpoint interno!");
    }

    private async Task TestAdminAsync()
    {
        Console.WriteLine("→ A tentar /api/admin/queries ...");
        Console.WriteLine("  (esperado: 403 Forbidden, pois este token não é Admin)");
        Console.WriteLine();

        var (status, body) = await _weather.CallAdminQueriesAsync();

        Console.WriteLine($"Estado HTTP: {(int)status} {status}");
        Console.WriteLine($"Corpo: {body}");

        if (status == HttpStatusCode.Forbidden)
            PrintSuccess("Comportamento correto: token externo rejeitado no endpoint admin.");
        else if (status == HttpStatusCode.OK)
            PrintError("FALHA DE SEGURANÇA: token externo conseguiu aceder ao endpoint admin!");
    }
}