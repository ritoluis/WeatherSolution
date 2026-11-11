# MQTT

Este documento descreve a integração MQTT do WeatherSolution: o broker, os tópicos, o fluxo de leitura sob pedido, a autenticação, o diagnóstico de erros e o mecanismo de rotação de chaves.

## 1. Visão geral

O sistema usa MQTT para comunicação bidirecional entre a API e os dispositivos ESP32. O broker é o Eclipse Mosquitto, que corre num contentor Docker no servidor.

A API liga-se ao broker como cliente e:

- Publica comandos nos tópicos dos dispositivos.
- Subscreve o tópico de leituras de todos os dispositivos.

Os dispositivos ligam-se ao broker e:

- Subscrevem o seu próprio tópico de comandos.
- Publicam leituras no seu tópico de leituras.

## 2. Porque MQTT e não polling

Com polling HTTP, a API teria de perguntar ao dispositivo de X em X segundos se tinha leitura nova. Isto desperdiça recursos e introduz latência.

Com MQTT:

- O dispositivo mantém uma ligação persistente ao broker.
- A API publica um comando quando precisa.
- O broker entrega o comando ao dispositivo imediatamente.
- O dispositivo responde no mesmo canal.

A latência é de segundos, e não há desperdício de pedidos quando ninguém está a consultar.

## 3. Broker

| Item | Valor |
|---|---|
| Imagem | eclipse-mosquitto:2.0 |
| Porta | 1883 (TCP) |
| Autenticação | Utilizador e password (ficheiro passwd.txt) |
| Configuração | mosquitto/config/mosquitto.conf |

A configuração do broker:

listener 1883 0.0.0.0
allow_anonymous false
password_file /mosquitto/config/passwd.txt

log_dest stdout
log_type error
log_type warning
log_type notice
log_type information

O listener 0.0.0.0 é essencial para que o broker aceite ligações de fora do contentor (nomeadamente, dos dispositivos na internet).

## 4. Utilizadores do broker

O ficheiro passwd.txt tem um utilizador por cada cliente:

| Utilizador | Para quem | Onde é usada a password |
|---|---|---|
| WeatherApi | A API | appsettings.json / variável de ambiente |
| SensorJardim | O ESP32 do Sensor Jardim | Sketch do ESP32 / NVS |

As passwords são criadas com mosquitto_passwd e guardadas como hash no passwd.txt. O ficheiro não vai para o Git.

### Permissões do passwd.txt

O ficheiro tem de ser legível pelo UID 1883 (o utilizador do Mosquitto dentro do contentor). Se as permissões estiverem erradas, o Mosquitto não consegue abrir o ficheiro, e o broker arranca sem autenticação funcional (ou rejeita todos os utilizadores).

Verificação:

sudo ls -l /opt/weather/mosquitto/config/passwd.txt

O output correto deve ser:

-rw-r--r-- 1 1883 1883 <tamanho> <data> passwd.txt

Correção, se necessário:

sudo chown 1883:1883 /opt/weather/mosquitto/config/passwd.txt
sudo chmod 644 /opt/weather/mosquitto/config/passwd.txt
sudo docker compose restart mosquitto

## 5. Tópicos

| Tópico | Direção | Payload |
|---|---|---|
| devices/{id}/commands | API → dispositivo | read_now |
| devices/{id}/readings | Dispositivo → API | Temperatura (ex: 22.7) |

O {id} é o DeviceId na base de dados (por exemplo, 1 para o Sensor Jardim).

A API subscreve devices/+/readings (o + é um wildcard de um nível), o que lhe permite receber leituras de todos os dispositivos.

## 6. Fluxo de leitura sob pedido

1. O admin clica em Ler Agora no dashboard.
2. O AdminController chama IMqttService.RequestReadingAsync(deviceId, timeout).
3. O MqttService cria uma TaskCompletionSource para o deviceId e publica read_now em devices/{id}/commands.
4. O broker entrega read_now ao dispositivo, que está subscrito.
5. O dispositivo lê o sensor e publica a temperatura em devices/{id}/readings.
6. O MqttService recebe a mensagem, extrai o deviceId e a temperatura, e completa a TaskCompletionSource.
7. O endpoint devolve 200 com a temperatura, ou 504 se o timeout (10 segundos) expirar.

## 7. Implementação do MqttService

O MqttService é um IHostedService e um Singleton. É registado no Program.cs de forma a que a mesma instância seja usada pelos controladores e pelo serviço em segundo plano.

| Funcionalidade | Como funciona |
|---|---|
| Ligação ao broker | No StartAsync, com as opções do appsettings.json |
| Subscrição | No evento ConnectedAsync, subscreve devices/+/readings |
| Receção de mensagens | No evento ApplicationMessageReceivedAsync, extrai o deviceId e a temperatura, e completa a TaskCompletionSource correspondente |
| Publicação | No RequestReadingAsync, com MqttApplicationMessageBuilder |
| Reconexão | No evento DisconnectedAsync, tenta reconectar após 5 segundos |
| Timeout | No RequestReadingAsync, com Task.WhenAny entre a TaskCompletionSource e um Task.Delay |

O payload pode vir em dois formatos: JSON ({"temperature": 22.7}) ou texto simples (22.7). O serviço trata ambos.

### Cliente MQTT no ESP32 (esp-mqtt)

O ESP32 usa o esp-mqtt, a biblioteca oficial da Espressif, incluída no core do Arduino-ESP32. A configuração é feita em iniciarMqtt():

esp_mqtt_client_config_t cfg = {};
cfg.broker.address.uri = "mqtt://ritoluis.duckdns.org:1883";
cfg.credentials.client_id = "ESP32-{mac}-{contador}";
cfg.credentials.username = "SensorJardim";
cfg.credentials.authentication.password = "...";
cfg.session.keepalive = 60;
cfg.network.disable_auto_reconnect = false;
cfg.network.reconnect_timeout_ms = 10000;

O cliente é registado com um handler de eventos (mqtt_event_handler), que trata MQTT_EVENT_CONNECTED (subscreve devices/{id}/commands), MQTT_EVENT_DISCONNECTED (apaga o LED MQTT), e MQTT_EVENT_DATA (processa read_now e send_now).

O clientId inclui um contador (ESP32-{mac}-{n}), para garantir que cada tentativa de ligação tem um ID único. Isto evita que o broker feche a ligação antiga quando o ESP32 se reconecta após um reinício.

A reconexão é automática (disable_auto_reconnect = false), gerida pela biblioteca. Não é necessário um ensureMqtt() nem um mqtt.loop() no loop() principal.

## 8. Autenticação e diagnóstico de erros

O PubSubClient (no ESP32) devolve um código de erro no método state(). Os códigos mais comuns:

| Código | Significado | O que verificar |
|---|---|---|
| -4 | MQTT_CONNECTION_TIMEOUT | O servidor não respondeu. Firewall ou port forwarding. |
| -3 | MQTT_CONNECTION_LOST | A ligação caiu. Wi-Fi instável. |
| -2 | MQTT_CONNECT_FAILED | Falha de rede ou DNS. O ESP32 não chega ao servidor. |
| -1 | MQTT_DISCONNECTED | Desligado. |
| 0 | MQTT_CONNECTED | Ligado. |
| 1 | MQTT_CONNECT_BAD_PROTOCOL | Protocolo errado. |
| 2 | MQTT_CONNECT_BAD_CLIENT_ID | Client ID errado. |
| 3 | MQTT_CONNECT_UNAVAILABLE | Servidor indisponível. |
| 4 | MQTT_CONNECT_BAD_CREDENTIALS | Credenciais erradas. |
| 5 | MQTT_CONNECT_UNAUTHORIZED | Não autorizado. Password errada ou ausente. |

O código 5 é o mais comum quando a password do MQTT está errada ou ausente no JSON enviado pela app Android. Nesse caso, a app deve enviar a password correta, ou o ESP32 deve manter a password atual da NVS (ver secção 10).

## 9. Rotação de chaves

O mecanismo de rotação permite mudar a API Key de um dispositivo sem intervenção física.

### Fluxo

1. O admin clica em Regenerar no dashboard.
2. A API gera uma nova chave, guarda o hash em PendingApiKeyHash, cifra a chave em claro com AES e guarda em PendingApiKeyEncrypted, e marca KeyRotationPending=true.
3. O dispositivo, no próximo ciclo de polling, chama GET /api/device/config.
4. A API devolve a nova chave em claro (a partir da versão cifrada).
5. O dispositivo guarda a nova chave na NVS e chama POST /api/device/confirm-key com a nova chave.
6. A API promove a chave pendente a chave atual, e limpa KeyRotationPending.

Durante a rotação, o dispositivo continua a autenticar-se com a chave antiga até confirmar a nova. Isto garante que uma falha a meio não deixa o dispositivo inoperante.

### Cifra da chave pendente

A chave pendente é cifrada com AES, usando uma chave derivada do Jwt:Key (SHA256). O IV é gerado aleatoriamente a cada rotação.

A chave em claro nunca é guardada na base de dados; só existe cifrada, e só é decifrada quando o dispositivo a pede.

## 10. Configuração do MQTT pela app Android

A app Android envia o JSON de configuração com os campos do MQTT:

{
  "mqtt": {
    "server": "...",
    "port": 1883,
    "user": "...",
    "password": "..."
  }
}

Se o campo mqtt.password vier vazio, o ESP32 mantém a password atual da NVS. Isto permite ao utilizador alterar o Wi-Fi sem ter de reescrever a password do MQTT.

A app pré-preenche o server, port e user a partir dos logs enviados pelo ESP32. A password fica vazia (não é enviada nos logs, por segurança), e o utilizador pode deixá-la vazia (mantém a atual) ou escrever uma nova.

## 11. Segurança

- O broker exige autenticação (allow_anonymous false).
- Cada dispositivo tem o seu próprio utilizador no broker.
- A API tem o seu próprio utilizador no broker.
- O ficheiro passwd.txt não vai para o Git.
- O ficheiro passwd.txt tem de ser legível pelo UID 1883.
- A porta 1883 está exposta à internet através de port forwarding, mas apenas com autenticação.
- A chave pendente é cifrada com AES antes de ser guardada.

## 12. Limitações atuais

O MQTT não usa TLS (porta 1883, não 8883). O tráfego entre o dispositivo e o broker circula em claro na internet. Para produção com dados sensíveis, deveria usar-se MQTT sobre TLS.

O mecanismo de polling de configuração é feito pelo dispositivo uma vez por dia. Se o admin regenerar a chave e o dispositivo estiver desligado, a nova chave só é adotada quando o dispositivo voltar a ligar.

O broker não tem ACLs (access control lists). Qualquer utilizador autenticado pode publicar em qualquer tópico. Para produção multi-tenant, deveriam configurar-se ACLs por utilizador.

## 13. Ficheiros principais

| Ficheiro | Função |
|---|---|
| WeatherApi/Services/MqttService.cs | Cliente MQTT (ligação, publicação, subscrição, reconexão) |
| WeatherApi/Controllers/DeviceController.cs | Endpoints /api/device/* |
| WeatherApi/Controllers/AdminController.cs | Endpoint request-reading |
| mosquitto/config/mosquitto.conf | Configuração do broker |
| mosquitto/config/passwd.txt | Utilizadores do broker (não versionado) |

## 14. Problemas conhecidos e soluções

| Problema | Causa | Solução |
|---|---|---|
| Ciclo de already connected, closing old connection | PubSubClient a reconectar antes de o broker fechar a ligação antiga | Migrar para esp-mqtt; definir clientId único por tentativa |
| has exceeded timeout, disconnecting | Keepalive não enviado a tempo; NTP a saltar o relógio depois de o MQTT arrancar | Sincronizar NTP antes de iniciar o MQTT (ordem no setup()) |
| rc=5 (MQTT_CONNECT_UNAUTHORIZED) | Password do MQTT errada ou ausente no JSON | Verificar o campo mqtt.password no formulário da app; o ESP32 mantém a password atual se o campo vier vazio |
| rc=-2 (MQTT_CONNECT_FAILED) | Falha de rede ou DNS | Verificar Wi-Fi, DNS e port forwarding da 1883 |