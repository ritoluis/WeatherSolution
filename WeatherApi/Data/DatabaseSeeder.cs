using Microsoft.EntityFrameworkCore;
using WeatherApi.Entities;
using WeatherApi.Services;

namespace WeatherApi.Data;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<WeatherDbContext>();
        var apiKeys = scope.ServiceProvider.GetRequiredService<IApiKeyService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        await db.Database.MigrateAsync();

        // ============ Admin ============
        if (!await db.Users.AnyAsync(u => u.Username == "admin"))
        {
            db.Users.Add(new User
            {
                Username = "admin",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("1978?Capicua"),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seed: admin criado.");
        }

        // ============ Utilizador interno ============
        if (!await db.Users.AnyAsync(u => u.Username == "joao"))
        {
            db.Users.Add(new User
            {
                Username = "joao",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("1978?Capicua"),
                Role = "User",
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seed: utilizador interno 'joao' criado.");
        }

        // ============ Utilizador externo ============
        if (!await db.Users.AnyAsync(u => u.Username == "agente01"))
        {
            db.Users.Add(new User
            {
                Username = "agente01",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("1978?Capicua"),
                Role = "User",
                CreatedAt = DateTime.UtcNow
            });
            logger.LogInformation("Seed: utilizador externo 'agente01' criado.");
        }

        await db.SaveChangesAsync();

        // ============ Dispositivos ============
        // Só cria dispositivos se não existir nenhum
        if (!await db.Devices.AnyAsync())
        {
            var devices = new[]
            {
                new { Name = "Sensor Jardim",  Location = "Lisboa - Jardim",   IsActive = true },
                new { Name = "Sensor Estufa",  Location = "Lisboa - Estufa",   IsActive = true },
                new { Name = "Sensor Garagem", Location = "Lisboa - Garagem",  IsActive = false }
            };

            var createdKeys = new List<(string Name, string ApiKey)>();

            foreach (var d in devices)
            {
                var apiKey = apiKeys.GenerateApiKey();
                createdKeys.Add((d.Name, apiKey));

                db.Devices.Add(new Device
                {
                    Name = d.Name,
                    Location = d.Location,
                    ApiKeyHash = apiKeys.HashApiKey(apiKey),
                    IsActive = d.IsActive,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await db.SaveChangesAsync();

            // Mostra as chaves em claro no log. Em desenvolvimento, isto é aceitável.
            // Em produção, esta informação NUNCA deve aparecer no log.
            logger.LogWarning("=======================================================");
            logger.LogWarning("  DISPOSITIVOS DE TESTE CRIADOS — GUARDE ESTAS CHAVES    ");
            logger.LogWarning("=======================================================");
            foreach (var (name, key) in createdKeys)
            {
                logger.LogWarning("  {Name}: {ApiKey}", name, key);
            }
            logger.LogWarning("=======================================================");
        }

        // ============ Leituras dos últimos 3 dias ============
        // Apenas para dispositivos ativos
        var activeDevices = await db.Devices
            .Where(d => d.IsActive)
            .ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var random = new Random();

        foreach (var device in activeDevices)
        {
            for (int i = 1; i <= 3; i++)
            {
                var date = today.AddDays(-i);

                // Verifica se já existe leitura
                var exists = await db.DeviceReadings
                    .AnyAsync(r => r.DeviceId == device.Id && r.ReadingDate == date);

                if (exists) continue;

                // Valor entre 12 e 28 graus, com casas decimais
                var temp = Math.Round((decimal)(random.NextDouble() * 16 + 12), 2);

                db.DeviceReadings.Add(new DeviceReading
                {
                    DeviceId = device.Id,
                    ReadingDate = date,
                    AverageTemperature = temp,
                    ReceivedAt = date.AddDays(1).ToDateTime(new TimeOnly(0, 5, 0))
                });

                logger.LogInformation(
                    "Seed: leitura {Date} para {Device}: {Temp} °C",
                    date, device.Name, temp);
            }
        }

        await db.SaveChangesAsync();

        // ============ Agregação das médias diárias ============
        // Para cada dia com leituras, recalcula a média agregada (TemperatureReadings)
        var datesToAggregate = await db.DeviceReadings
            .Select(r => r.ReadingDate)
            .Distinct()
            .ToListAsync();

        var temperatureService = scope.ServiceProvider.GetRequiredService<ITemperatureService>();

        foreach (var date in datesToAggregate)
        {
            await temperatureService.RecalculateDailyAverageAsync(date);
        }

        logger.LogInformation("Seed concluído: {DeviceCount} dispositivos, {Dates} dias agregados.",
            activeDevices.Count, datesToAggregate.Count);
    }
}