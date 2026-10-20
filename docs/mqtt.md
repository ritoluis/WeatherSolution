# MQTT

Este documento descreve a integração MQTT do WeatherSolution: o broker, os tópicos, o fluxo de leitura sob pedido, e o mecanismo de rotação de chaves.

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

text

O listener 0.0.0.0 é essencial para que o broker aceite ligações de fora do contentor (nomeadamente, dos dispositivos na internet).

## 4. Utilizadores do broker

O ficheiro passwd.txt tem um utilizador por cada cliente:

| Utilizador | Para quem | Onde é usada a password |
|---|---|---|
| WeatherApi | A API | appsettings.json / variável de ambiente |
| SensorJardim | O ESP32 do Sensor Jardim | Sketch do ESP32 |

As passwords são criadas com mosquitto_passwd e guardadas como hash no passwd.txt. O ficheiro não vai para o Git.

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

## 8. Rotação de chaves

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

## 9. Segurança

- O broker exige autenticação (allow_anonymous false).
- Cada dispositivo tem o seu próprio utilizador no broker.
- A API tem o seu próprio utilizador no broker.
- O ficheiro passwd.txt não vai para o Git.
- A porta 1883 está exposta à internet através de port forwarding, mas apenas com autenticação.
- A chave pendente é cifrada com AES antes de ser guardada.

## 10. Limitações atuais

- O MQTT não usa TLS (porta 1883, não 8883). O tráfego entre o dispositivo e o broker circula em claro na internet. Para produção com dados sensíveis, deveria usar-se MQTT sobre TLS.
- O mecanismo de polling de configuração é feito pelo dispositivo uma vez por dia. Se o admin regenerar a chave e o dispositivo estiver desligado, a nova chave só é adotada quando o dispositivo voltar a ligar.
- O broker não tem ACLs (access control lists). Qualquer utilizador autenticado pode publicar em qualquer tópico. Para produção multi-tenant, deveriam configurar-se ACLs por utilizador.

## 11. Ficheiros principais

| Ficheiro | Função |
|---|---|
| WeatherApi/Services/MqttService.cs | Cliente MQTT (ligação, publicação, subscrição, reconexão) |
| WeatherApi/Controllers/DeviceController.cs | Endpoints /api/device/* |
| WeatherApi/Controllers/AdminController.cs | Endpoint request-reading |
| mosquitto/config/mosquitto.conf | Configuração do broker |
| mosquitto/config/passwd.txt | Utilizadores do broker (não versionado) |