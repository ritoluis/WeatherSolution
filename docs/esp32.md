# ESP32

Este documento descreve o dispositivo ESP32: o hardware, as ligações, o firmware, a sincronização de hora, o envio diário, o modo configuração BLE e a persistência da configuração.

## 1. Visão geral

O dispositivo é um ESP32 WROOM-32D, ligado a um sensor de temperatura DHT11. Comunica com o servidor por três canais:

- MQTT, para receber comandos e responder com leituras imediatas.
- HTTPS, para enviar a média diária à API.
- BLE, para configuração local (Wi-Fi, MQTT, API) através da app Android.

O firmware é escrito em C++ com o framework Arduino, e gravado com o Arduino IDE.

## 2. Hardware

| Componente | Pinos | Notas |
|---|---|---|
| ESP32 WROOM-32D | — | Placa com Wi-Fi 2.4 GHz e BLE |
| DHT11 | GPIO 25 (DATA) | Requer pull-up de 10kΩ (o módulo já traz) |
| DHT11 | 3.3V (VCC) | Nunca 5V, pode danificar o sensor |
| DHT11 | GND | — |
| LED Power | GPIO 26 | Aceso desde o arranque |
| LED MQTT | GPIO 27 | Aceso quando o MQTT liga |
| LED Wi-Fi | GPIO 32 | Aceso quando o Wi-Fi liga |
| LED Config | GPIO 33 | Pisca no modo configuração, fixo quando a app conecta |
| Botão Config | GPIO 14 | INPUT_PULLUP, ligado entre GPIO 14 e GND |

O ESP32 é alimentado pelo cabo USB do PC ou por uma fonte USB de 5V.

## 3. Estrutura do firmware

| Funcionalidade | Como funciona |
|---|---|
| Ligação Wi-Fi | WiFi.begin com SSID e password da NVS |
| Ligação MQTT | PubSubClient, com cliente que se reconecta automaticamente |
| Sincronização de hora | NTP (pool.ntp.org), em UTC |
| Leitura de temperatura | DHT11, ou valor simulado (modo de simulação) |
| Acumulação horária | Soma e contagem das leituras do dia |
| Envio diário | Às 00:05 UTC, calcula a média do dia anterior e envia via HTTPS |
| Leitura sob pedido | Recebe read_now via MQTT e responde em devices/{id}/readings |
| Rotação de chave | Polling diário a /api/device/config, guarda a nova chave na NVS |
| Modo configuração BLE | Botão pressionado entra em modo BLE, recebe JSON, guarda na NVS |
| Persistência | Configuração e API Key guardadas na NVS (Preferences) |

## 4. Configuração na NVS

A configuração é guardada na NVS em duas chaves:

| Chave | Conteúdo |
|---|---|
| config | JSON com wifi, mqtt, api, deviceId |
| apiKey | API Key do dispositivo (separada do JSON) |

O JSON da chave config tem esta estrutura:

{
  "wifi": { "ssid": "...", "password": "..." },
  "mqtt": { "server": "...", "port": 1883, "user": "...", "password": "..." },
  "api": { "host": "...", "port": 443 },
  "deviceId": 1
}

A API Key está numa chave separada porque não é configurável pela app Android. É gerida pelo servidor e pelo mecanismo de rotação de chaves.

## 5. Valores por defeito

Se a NVS estiver vazia, o firmware usa os valores por defeito definidos no topo do sketch:

| Constante | Valor |
|---|---|
| DEFAULT_WIFI_SSID | MEO-D775E0 |
| DEFAULT_WIFI_PASSWORD | (password da rede) |
| DEFAULT_MQTT_SERVER | ritoluis.duckdns.org |
| DEFAULT_MQTT_PORT | 1883 |
| DEFAULT_MQTT_USER | SensorJardim |
| DEFAULT_MQTT_PASSWORD | (password do broker) |
| DEFAULT_API_HOST | ritoluis.duckdns.org |
| DEFAULT_API_PORT | 443 |
| DEFAULT_DEVICE_ID | 1 |
| DEFAULT_API_KEY | (chave de bootstrap) |

## 6. Modo configuração BLE

### Como entrar

O botão de configuração (GPIO 14) pode ser pressionado em dois momentos:

- No arranque: o setup() verifica o botão antes de ligar o Wi-Fi. Se estiver pressionado, entra em modo configuração.
- A qualquer momento: o loop() verifica o botão no início de cada iteração. Se estiver pressionado, interrompe o loop e entra em modo configuração.

Durante a tentativa de ligação Wi-Fi, o ligarWiFi() também verifica o botão a cada 100 ms. Isto garante que, mesmo que o ESP32 esteja preso a tentar ligar a uma rede inexistente, o botão funciona.

### O que acontece

1. O Wi-Fi é desligado (WiFi.disconnect(true) + WiFi.mode(WIFI_OFF)), para libertar o rádio para o BLE.
2. O LED Config começa a piscar (500 ms aceso, 500 ms apagado).
3. O BLE arranca com o nome PROV-Sensor{id}.
4. O serviço A001 e as três características (A002 config, A003 log, A004 cancel) são criados.
5. O ESP32 fica à espera de conexão durante 5 minutos.

### Quando a app conecta

1. O LED Config fica fixo.
2. O ESP32 envia os dados atuais via logs: SSID, MQTT server, MQTT port, MQTT user, API host, API port, Device ID.
3. A app pré-preenche o formulário com esses valores.
4. O utilizador altera o que precisar e envia o JSON.

### Quando o JSON é recebido

1. O ESP32 valida o JSON (wifi.ssid é obrigatório).
2. Para cada campo, se o valor vier vazio ou ausente no JSON, mantém o valor atual da NVS. Só sobrescreve os campos que vierem preenchidos.
3. Guarda o JSON final na NVS.
4. Envia um log de confirmação à app.
5. Aguarda 1,5 segundos e reinicia (ESP.restart()).

### Quando a app cancela

1. O ESP32 recebe o comando cancel na característica A004.
2. Envia um log de confirmação.
3. Aguarda 0,5 segundos e reinicia, sem alterar a NVS.

### Timeouts

| Evento | Timeout |
|---|---|
| Modo configuração sem conexão | 5 minutos |
| Conexão sem JSON recebido | 5 minutos |

O keepalive BLE envia uma notificação vazia a cada 10 segundos, para manter a conexão viva enquanto a app está ligada.

## 7. Sincronização de hora (NTP)

O ESP32 não tem relógio interno com bateria. Ao arrancar, sincroniza a hora com um servidor NTP:

configTime(0, 0, "pool.ntp.org", "time.nist.gov");

O offset 0, 0 significa UTC puro. O ESP32 não aplica o fuso horário de Portugal; todas as datas e horas são em UTC.

O NTP é re-sincronizado a cada 6 horas, para evitar desvios.

## 8. Leitura horária

A cada hora, o ESP32:

1. Lê a temperatura (real ou simulada).
2. Adiciona o valor à soma do dia (todayReadings.sum).
3. Incrementa o contador (todayReadings.count).

A leitura inicial acontece no setup, imediatamente após a sincronização NTP.

## 9. Envio diário

Às 00:05 UTC, o ESP32:

1. Verifica se tem leituras acumuladas (count > 0).
2. Calcula a média (sum / count).
3. A data é a do dia anterior (o dia que terminou).
4. Envia via HTTPS para /api/device/reading, com a API Key no header X-Device-Key.
5. Se o envio falhar, guarda o valor como pendente e tenta novamente a cada hora.

O corpo do pedido:

{ "date": "2026-10-19", "averageTemperature": 21.5 }

## 10. Leitura sob pedido (MQTT)

Quando a API publica read_now em devices/{id}/commands, o ESP32:

1. Recebe a mensagem no callback MQTT.
2. Lê a temperatura (real ou simulada).
3. Publica o valor em devices/{id}/readings.

A leitura sob pedido não entra na média diária. É apenas uma resposta instantânea ao admin.

## 11. Modo simulado

Enquanto o DHT11 esteve avariado, o firmware usou um modo simulado:

const bool USE_SIMULATED_READING = true;

Neste modo, a função readTemperature devolve um valor aleatório entre 18.0 e 28.0, em vez de ler o sensor.

Quando o sensor real for substituído, basta mudar para false. Nenhum outro código muda.

## 12. Persistência da API Key (NVS)

A API Key é guardada na NVS (Non-Volatile Storage) do ESP32, através da biblioteca Preferences, na chave apiKey (separada do JSON de configuração).

No primeiro arranque, a NVS está vazia, e o firmware usa a chave de bootstrap (DEFAULT_API_KEY). A partir daí, a chave é sempre lida da NVS.

Se o admin regenerar a chave no dashboard, o dispositivo adota-a no próximo polling a /api/device/config. A app Android não vê nem altera a API Key.

## 13. Rotação de chave

O polling de configuração corre uma vez por dia (e no arranque). O fluxo:

1. O ESP32 chama GET /api/device/config com a chave atual.
2. A API devolve o estado do dispositivo e, se KeyRotationPending=true, a nova chave em claro.
3. O ESP32 guarda a nova chave na NVS, temporariamente.
4. O ESP32 chama POST /api/device/confirm-key com a nova chave.
5. Se a API confirmar (200), a nova chave passa a ser a atual.

Se o confirm falhar, o ESP32 mantém a chave antiga e tenta novamente no próximo ciclo.

## 14. Reconexão

| Situação | Comportamento |
|---|---|
| Wi-Fi cai | ensureWifi tenta reconectar a cada 10 segundos |
| MQTT cai | ensureMqtt tenta reconectar a cada 5 segundos |
| NTP falha | syncTime tenta novamente a cada iteração do loop |

O LED MQTT apaga quando o MQTT cai, e acende quando volta a ligar. O LED Wi-Fi apaga quando o Wi-Fi cai, e acende quando volta a ligar.

## 15. Códigos de erro do MQTT

O PubSubClient devolve um código de erro no método state(). Os códigos mais comuns:

| Código | Significado |
|---|---|
| -4 | MQTT_CONNECTION_TIMEOUT (o servidor não respondeu) |
| -3 | MQTT_CONNECTION_LOST (a ligação caiu) |
| -2 | MQTT_CONNECT_FAILED (falha de rede ou DNS) |
| -1 | MQTT_DISCONNECTED (desligado) |
| 0 | MQTT_CONNECTED (ligado) |
| 1 | MQTT_CONNECT_BAD_PROTOCOL (protocolo errado) |
| 2 | MQTT_CONNECT_BAD_CLIENT_ID (client ID errado) |
| 3 | MQTT_CONNECT_UNAVAILABLE (servidor indisponível) |
| 4 | MQTT_CONNECT_BAD_CREDENTIALS (credenciais erradas) |
| 5 | MQTT_CONNECT_UNAUTHORIZED (não autorizado) |

O código 5 significa que a password do MQTT está errada ou ausente. O código -2 significa que o ESP32 não consegue chegar ao servidor (rede ou DNS).

## 16. Limitações conhecidas

O status=133 (GATT_ERROR) aparece na app Android quando o utilizador envia o JSON ou cancela. É cosmético: a operação funciona (o ESP32 recebe, guarda e reinicia), mas o Android não recebe a confirmação GATT a tempo, porque o ESP32 reinicia imediatamente após a escrita.

A causa provável é a coexistência de rádio no ESP32: o Wi-Fi e o BLE partilham o mesmo módulo de 2.4 GHz, e o Wi-Fi pode abafar o BLE durante a escrita. A API de coexistência da Espressif (ESP_COEX_PREFER_BT) não está disponível no core Arduino-ESP32 usado no projeto.

O MQTT não usa TLS (porta 1883). O tráfego entre o dispositivo e o broker circula em claro na internet.

O HTTPS usa client.setInsecure(), que não valida o certificado do servidor. Para produção, deveria usar setCACert com o certificado da Let's Encrypt (ISRG Root X1).

O envio diário usa HTTP, não MQTT. Se o servidor estiver em baixo às 00:05, o valor fica pendente e é tentado novamente.

O fuso horário é UTC. Se se quiser que o envio seja à meia-noite de Portugal, é preciso ajustar o configTime com o offset do fuso.

O cliente MQTT foi migrado do PubSubClient para o esp-mqtt (nativo do core ESP32). O PubSubClient tinha um bug conhecido: o connected() podia devolver false mesmo com a ligação ativa, levando a um ciclo de reconexões e ao fecho da ligação antiga pelo broker (already connected, closing old connection). O esp-mqtt gere a reconexão de forma estável, sem este ciclo.

## 17. Bibliotecas usadas

| Biblioteca | Função |
|---|---|
| WiFi.h | Ligação Wi-Fi |
| WiFiClientSecure.h | Ligação HTTPS |
| DHT.h | Leitura do DHT11 |
| time.h | Sincronização NTP |
| Preferences.h | Persistência na NVS |
| ArduinoJson.h | Parse e serialização do JSON |
| BLEDevice.h, BLEServer.h, BLEUtils.h, BLE2902.h | Servidor BLE |
| mqtt_client.h | Cliente MQTT (esp-mqtt, nativo do core ESP32) |

## 18. Ficheiros principais

| Ficheiro | Função |
|---|---|
| WeatherStationNode.ino | Sketch completo do ESP32 |

## 19. Ordem de arranque (NTP antes do MQTT)

No setup(), a ordem é importante:

1. Ligar ao Wi-Fi.
2. Sincronizar o NTP (syncTime()).
3. Iniciar o cliente MQTT (iniciarMqtt()).

Se o MQTT for iniciado antes do NTP, o esp-mqtt inicializa o temporizador de keepalive com o tempo do sistema (que está perto de 0). Quando o NTP sincroniza e salta o relógio para a data real, o esp-mqtt pensa que o keepalive expirou, e o broker desliga o cliente por timeout (has exceeded timeout, disconnecting).

Com o NTP a correr primeiro, o relógio já está estável quando o MQTT arranca, e o keepalive é enviado no intervalo correto (60 segundos).