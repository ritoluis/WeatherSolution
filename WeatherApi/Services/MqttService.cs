using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;

namespace WeatherApi.Services;



public class MqttService : IHostedService, IMqttService, IAsyncDisposable
{
    private readonly IConfiguration _config;
    private readonly ILogger<MqttService> _logger;
    private readonly IMqttClient _client;
    private readonly MqttClientOptions _options;

    // Pedidos pendentes: deviceId -> TaskCompletionSource que aguarda a leitura
    private readonly ConcurrentDictionary<int, TaskCompletionSource<decimal>> _pendingRequests = new();

    public MqttService(IConfiguration config, ILogger<MqttService> logger)
    {
        _config = config;
        _logger = logger;

        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        var clientId = _config["Mqtt:ClientId"] ?? "WeatherApi-Backend";

        _options = new MqttClientOptionsBuilder()
            .WithTcpServer(
                _config["Mqtt:BrokerHost"] ?? "localhost",
                int.Parse(_config["Mqtt:BrokerPort"] ?? "1883"))
            .WithClientId(clientId)
            .WithCredentials(
                _config["Mqtt:Username"],
                _config["Mqtt:Password"])
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(60))
            .Build();

        // Eventos
        _client.ConnectedAsync += OnConnectedAsync;
        _client.DisconnectedAsync += OnDisconnectedAsync;
        _client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
    }

    public bool IsConnected => _client.IsConnected;

    // ============ IHostedService ============

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _client.ConnectAsync(_options, cancellationToken);
            _logger.LogInformation("MqttService ligado ao broker {Host}:{Port}",
                _config["Mqtt:BrokerHost"], _config["Mqtt:BrokerPort"]);
        }
        catch (Exception ex)
        {
            // Não rebenta a app se o broker estiver em baixo.
            // O serviço tentará reconectar-se periodicamente.
            _logger.LogError(ex,
                "Falha ao ligar ao broker MQTT. A API continuará a funcionar sem MQTT.");
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (_client.IsConnected)
            {
                await _client.DisconnectAsync(
                    new MqttClientDisconnectOptionsBuilder()
                        .WithReason(MqttClientDisconnectOptionsReason.NormalDisconnection)
                        .Build(),
                    cancellationToken);
                _logger.LogInformation("MqttService desligado do broker.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Erro ao desligar do broker MQTT.");
        }
    }

    // ============ Eventos do cliente MQTT ============

    private async Task OnConnectedAsync(MqttClientConnectedEventArgs args)
    {
        _logger.LogInformation("Ligado ao broker MQTT.");

        // Subscreve o tópico de leituras de todos os dispositivos.
        // O '+' é um wildcard de um nível: devices/1/readings, devices/2/readings, ...
        var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(f => f
                .WithTopic("devices/+/readings")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
            .Build();

        await _client.SubscribeAsync(subscribeOptions);
        _logger.LogInformation("Subscrito em devices/+/readings.");
    }

    private Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs args)
    {
        _logger.LogWarning("Desligado do broker MQTT. Razão: {Reason}", args.Reason);

        // Tenta reconectar após alguns segundos (fire-and-forget)
        _ = Task.Run(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5));
            try
            {
                if (!_client.IsConnected)
                    await _client.ConnectAsync(_options);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao tentar reconectar ao broker MQTT.");
            }
        });

        return Task.CompletedTask;
    }

    private Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs args)
    {
        try
        {
            var topic = args.ApplicationMessage.Topic;
            var payload = Encoding.UTF8.GetString(
                args.ApplicationMessage.PayloadSegment.ToArray());

            _logger.LogDebug("MQTT recebido: {Topic} = {Payload}", topic, payload);

            // Tópico esperado: devices/{id}/readings
            var parts = topic.Split('/');
            if (parts.Length != 3 || parts[0] != "devices" || parts[2] != "readings")
                return Task.CompletedTask;

            if (!int.TryParse(parts[1], out var deviceId))
                return Task.CompletedTask;

            // O payload pode ser JSON {"temperature": 22.7} ou apenas "22.7"
            decimal? temperature = null;

            if (payload.TrimStart().StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("temperature", out var tempEl))
                    temperature = tempEl.GetDecimal();
            }
            else if (decimal.TryParse(payload,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var t))
            {
                temperature = t;
            }

            if (temperature == null)
            {
                _logger.LogWarning("Payload inválido de {Topic}: {Payload}", topic, payload);
                return Task.CompletedTask;
            }

            // Se há um pedido pendente para este device, completa-o
            if (_pendingRequests.TryRemove(deviceId, out var tcs))
            {
                tcs.TrySetResult(temperature.Value);
                _logger.LogInformation("Leitura imediata recebida do dispositivo {DeviceId}: {Temp} °C",
                    deviceId, temperature.Value);
            }
            else
            {
                _logger.LogDebug("Leitura recebida do dispositivo {DeviceId} sem pedido pendente.",
                    deviceId);
            }

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar mensagem MQTT.");
            return Task.CompletedTask;
        }
    }

    // ============ API pública para o AdminController ============

    public async Task<decimal?> RequestReadingAsync(int deviceId, TimeSpan timeout)
    {
        if (!_client.IsConnected)
        {
            _logger.LogWarning("Não é possível pedir leitura: MQTT não está ligado.");
            return null;
        }

        // Previne pedidos duplicados para o mesmo dispositivo
        if (_pendingRequests.ContainsKey(deviceId))
        {
            _logger.LogWarning("Já existe um pedido pendente para o dispositivo {DeviceId}.", deviceId);
            return null;
        }

        var tcs = new TaskCompletionSource<decimal>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[deviceId] = tcs;

        try
        {
            var topic = $"devices/{deviceId}/commands";

            // Na MQTTnet 4.x, o payload é construído com um builder
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload("read_now")
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .WithRetainFlag(false)
                .Build();

            await _client.PublishAsync(message);

            _logger.LogInformation("Comando 'read_now' publicado em {Topic}.", topic);

            // Espera pela resposta ou timeout
            var completedTask = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            if (completedTask == tcs.Task)
            {
                return await tcs.Task;
            }

            _logger.LogWarning("Timeout à espera da leitura do dispositivo {DeviceId}.", deviceId);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao pedir leitura do dispositivo {DeviceId}.", deviceId);
            return null;
        }
        finally
        {
            _pendingRequests.TryRemove(deviceId, out _);
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_client.IsConnected)
                await _client.DisconnectAsync();
            _client.Dispose();
        }
        catch
        {
            // ignorar erros no dispose
        }
    }
}