using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using WeatherExternalClient;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
    })
    .ConfigureServices((context, services) =>
    {
        var baseUrl = context.Configuration["ApiSettings:BaseUrl"]!;

        services.AddHttpClient<ITokenService, TokenService>(c =>
        {
            c.BaseAddress = new Uri(baseUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            // Aceita o certificado autoassinado de desenvolvimento.
            // REMOVER EM PRODUÇÃO.
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        });

        services.AddHttpClient<IWeatherClient, WeatherClient>(c =>
        {
            c.BaseAddress = new Uri(baseUrl);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        });

        services.AddTransient<Menu>();
    })
    .Build();

var menu = host.Services.GetRequiredService<Menu>();
await menu.RunAsync();