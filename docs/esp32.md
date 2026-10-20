# ESP32

Este documento descreve o dispositivo ESP32: o hardware, as ligações, o firmware, a sincronização de hora, o envio diário e o modo simulado.

## 1. Visão geral

O dispositivo é um ESP32 WROOM-32D, ligado a um sensor de temperatura DHT11. Comunica com o servidor por dois canais:

- MQTT, para receber comandos e responder com leituras imediatas.
- HTTPS, para enviar a média diária à API.

O firmware é escrito em C++ com o framework Arduino, e gravado com o Arduino IDE.

## 2. Hardware

| Componente | Pinos | Notas |
|---|---|---|
| ESP32 WROOM-32D | — | Placa com Wi-Fi 2.4 GHz |
| DHT11 | GPIO 25 (DATA) | Requer pull-up de 10kΩ (o módulo já traz) |
| DHT11 | 3.3V (VCC) | Nunca 5V, pode danificar o sensor |
| DHT11 | GND | — |
| LED Ligado | GPIO 26 | Aceso desde o arranque |
| LED Conectado | GPIO 27 | Aceso quando o MQTT liga |

O ESP32 é alimentado pelo cabo USB do PC ou por uma fonte USB de 5V.

## 3. Estrutura do firmware

| Funcionalidade | Como funciona |
|---|---|
| Ligação Wi-Fi | WiFi.begin com SSID e password |
| Ligação MQTT | PubSubClient, com cliente que se reconecta automaticamente |
| Sincronização de hora | NTP (pool.ntp.org), em UTC |
| Leitura de temperatura | DHT11, ou valor simulado (modo de simulação) |
| Acumulação horária | Soma e contagem das leituras do dia |
| Envio diário | Às 00:05 UTC, calcula a média do dia anterior e envia via HTTPS |
| Leitura sob pedido | Recebe read_now via MQTT e responde em devices/{id}/readings |
| Rotação de chave | Polling diário a /api/device/config, guarda a nova chave na NVS |
| Persistência | API Key guardada na NVS (Preferences) |

## 4. Configuração do sketch

As constantes no topo do sketch:

| Constante | Valor | Notas |
|---|---|---|
| WIFI_SSID | Nome da rede | — |
| WIFI_PASSWORD | Password da rede | — |
| MQTT_SERVER | ritoluis.duckdns.org | Domínio público do servidor |
| MQTT_PORT | 1883 | Porta MQTT |
| MQTT_USER | SensorJardim | Utilizador do broker |
| MQTT_PASSWORD | Password do broker | — |
| API_HOST | ritoluis.duckdns.org | Domínio público da API |
| API_PORT | 7119 ou 443 | Conforme o esquema (HTTPS ou HTTP) |
| DEVICE_ID | 1 | DeviceId na base de dados |
| USE_SIMULATED_READING | true | Ativa o modo simulado |

## 5. Sincronização de hora (NTP)

O ESP32 não tem relógio interno com bateria. Ao arrancar, sincroniza a hora com um servidor NTP:
configTime(0, 0, "pool.ntp.org", "time.nist.gov");

text

O offset 0, 0 significa UTC puro. O ESP32 não aplica o fuso horário de Portugal; todas as datas e horas são em UTC.

O NTP é re-sincronizado a cada 6 horas, para evitar desvios.

## 6. Leitura horária

A cada hora, o ESP32:

1. Lê a temperatura (real ou simulada).
2. Adiciona o valor à soma do dia (todayReadings.sum).
3. Incrementa o contador (todayReadings.count).

A leitura inicial acontece no setup, imediatamente após a sincronização NTP.

## 7. Envio diário

Às 00:05 UTC, o ESP32:

1. Verifica se tem leituras acumuladas (count > 0).
2. Calcula a média (sum / count).
3. A data é a do dia anterior (o dia que terminou).
4. Envia via HTTPS para /api/device/reading, com a API Key no header X-Device-Key.
5. Se o envio falhar, guarda o valor como pendente e tenta novamente a cada hora.

O corpo do pedido:
{ "date": "2026-10-19", "averageTemperature": 21.5 }

text

## 8. Leitura sob pedido (MQTT)

Quando a API publica read_now em devices/{id}/commands, o ESP32:

1. Recebe a mensagem no callback MQTT.
2. Lê a temperatura (real ou simulada).
3. Publica o valor em devices/{id}/readings.

A leitura sob pedido não entra na média diária. É apenas uma resposta instantânea ao admin.

## 9. Modo simulado

Enquanto o DHT11 esteve avariado, o firmware usou um modo simulado:
const bool USE_SIMULATED_READING = true;

text

Neste modo, a função readTemperature devolve um valor aleatório entre 18.0 e 28.0, em vez de ler o sensor.

Quando o sensor real for substituído, basta mudar para false. Nenhum outro código muda.

## 10. Persistência da API Key (NVS)

A API Key é guardada na NVS (Non-Volatile Storage) do ESP32, através da biblioteca Preferences. Isto permite que sobreviva a reinícios e atualizações de firmware.

No primeiro arranque, a NVS está vazia, e o firmware usa uma chave de bootstrap (definida no código). A partir daí, a chave é sempre lida da NVS.

Se o admin regenerar a chave no dashboard, o dispositivo adota-a no próximo polling a /api/device/config.

## 11. Rotação de chave

O polling de configuração corre uma vez por dia (e no arranque). O fluxo:

1. O ESP32 chama GET /api/device/config com a chave atual.
2. A API devolve o estado do dispositivo e, se KeyRotationPending=true, a nova chave em claro.
3. O ESP32 guarda a nova chave na NVS, temporariamente.
4. O ESP32 chama POST /api/device/confirm-key com a nova chave.
5. Se a API confirmar (200), a nova chave passa a ser a atual.

Se o confirm falhar, o ESP32 mantém a chave antiga e tenta novamente no próximo ciclo.

## 12. Reconexão

| Situação | Comportamento |
|---|---|
| Wi-Fi cai | ensureWifi tenta reconectar a cada 10 segundos |
| MQTT cai | ensureMqtt tenta reconectar a cada 5 segundos |
| NTP falha | syncTime tenta novamente a cada iteração do loop |

O LED Conectado (GPIO 27) apaga quando o MQTT cai, e acende quando volta a ligar.

## 13. Limitações atuais

- O HTTPS usa client.setInsecure(), que não valida o certificado do servidor. Para produção, deveria usar setCACert com o certificado da Let's Encrypt (ISRG Root X1).
- O MQTT não usa TLS (porta 1883). O tráfego circula em claro na internet.
- O envio diário usa HTTP, não MQTT. Se o servidor estiver em baixo às 00:05, o valor fica pendente e é tentado novamente.
- O fuso horário é UTC. Se se quiser que o envio seja à meia-noite de Portugal, é preciso ajustar o configTime com o offset do fuso.

## 14. Bibliotecas usadas

| Biblioteca | Função |
|---|---|
| WiFi.h | Ligação Wi-Fi |
| WiFiClientSecure.h | Ligação HTTPS |
| PubSubClient.h | Cliente MQTT |
| DHT.h | Leitura do DHT11 |
| time.h | Sincronização NTP |
| Preferences.h | Persistência na NVS |

## 15. Ficheiros principais

| Ficheiro | Função |
|---|---|
| WeatherStationNode.ino | Sketch completo do ESP32 |