# Contexto do Projeto WeatherSolution

Este documento resume o estado do projeto, para ser colado no início de uma nova conversa com um assistente, de forma a retomar o contexto sem repetir perguntas.

## O que é

Sistema de recolha e consulta de temperaturas médias diárias, composto por três aplicações .NET 9, uma app Android de configuração e um dispositivo ESP32:

| Componente | Tipo | Função |
|---|---|---|
| WeatherApi | ASP.NET Core Web API | Endpoints REST, autenticação JWT + API Key, acesso a MySQL, integração MQTT |
| WeatherWebApp | ASP.NET Core MVC + Razor | Interface web para utilizadores internos e admin |
| WeatherExternalClient | Consola .NET | Simulador de agente externo (testa a API pública) |
| AppWeatherSolutionConfig | App Android (Kotlin + Compose) | Configuração do ESP32 via BLE (Wi-Fi, MQTT, API) |
| ESP32 WROOM-32D | Firmware Arduino | Envia leituras diárias via HTTPS, responde a pedidos via MQTT, configura-se via BLE |

## Estado atual (produção)

Servidor: Debian 13 (Trixie), amd64, i3, 4GB RAM, sem GUI, acesso SSH com utilizador luis (sudo).

Localização: /opt/weather.

Docker Compose com 5 serviços: weather-mysql, weather-mosquitto, weather-api, weather-webapp, weather-nginx.

Acesso externo: https://ritoluis.duckdns.org (DuckDNS + Let's Encrypt).

Certificado: válido até 16/01/2027, renovação automática via cron (/opt/weather/renew-cert.sh, 2x/dia).

Backup BD: cron diário às 03:00 (/opt/weather/backup-db.sh, retenção 30 dias, em /opt/weather/backups/).

DuckDNS: atualização a cada 5 min (/opt/weather/duckdns-update.sh, verifica resposta OK/KO).

Port forwarding no router MEO: 80, 443, 1883 → 192.168.1.86 (lease estática).

## Modelo de permissões

| Ator | Dia anterior | Várias datas | Auditoria | Gerir dispositivos |
|---|---|---|---|---|
| Agente externo (client_type=external) | Sim | Não | Não | Não |
| Utilizador interno (client_type=internal, User) | Sim | Sim | Não | Não |
| Admin (client_type=internal, Admin) | Sim | Sim | Sim | Sim |
| Dispositivo (API Key) | — | — | — | — (só envia) |

## Base de dados (MySQL 8.0)

Tabelas: users, devices, devicereadings, temperaturereadings, userqueries.

Importante: as tabelas estão em minúsculas no Linux. O WeatherDbContext tem ToTable("users"), ToTable("devices"), etc., para compatibilidade Windows/Linux. Não alterar sem motivo.

Relações: User → UserQuery (cascade), Device → DeviceReading (cascade). Índice único em (DeviceId, ReadingDate).

## Autenticação

JWT para utilizadores. Claims: nameid, unique_name, role, client_type (internal/external), exp, iss, aud.

API Key para dispositivos, header X-Device-Key, guardada como hash BCrypt.

Rotação de chave: admin regenera → PendingApiKeyHash + versão cifrada AES → dispositivo faz polling a /api/device/config → confirma com /api/device/confirm-key.

Rate limiting e bloqueio de conta (3 tentativas falhadas → 5 min bloqueado).

## MQTT

Broker: Eclipse Mosquitto 2.0 em Docker, porta 1883.

Utilizadores no broker: WeatherApi (API) e SensorJardim (dispositivo), em passwd.txt (não versionado, legível pelo UID 1883).

Tópicos: devices/{id}/commands (API → device, payload read_now) e devices/{id}/readings (device → API, payload temperatura).

Fluxo "Ler Agora": admin → API publica read_now → device responde → API devolve ao browser (timeout 10s, 504 se falhar).

Diagnóstico de erros: rc=-2 falha de rede/DNS, rc=-4 timeout, rc=5 credenciais erradas.

## ESP32

Pinos: DHT11 no GPIO 25, LED Power no GPIO 26, LED MQTT no GPIO 27, LED Wi-Fi no GPIO 32, LED Config no GPIO 33, Botão Config no GPIO 14 (INPUT_PULLUP).

DHT11 avariado: USE_SIMULATED_READING = true no sketch. Quando o sensor novo chegar, mudar para false.

NTP em UTC. Leitura horária acumulada. Envio diário às 00:05 UTC para /api/device/reading.

Configuração guardada na NVS em duas chaves: config (JSON com wifi, mqtt, api, deviceId) e apiKey (separada).

Valores por defeito no topo do sketch (DEFAULT_WIFI_SSID, etc.), usados se a NVS estiver vazia.

Modo configuração BLE: botão pressionado (no arranque ou a qualquer momento) → Wi-Fi desligado → BLE arranca com nome PROV-Sensor{id} → app conecta → recebe JSON → guarda na NVS → reinicia.

Campos vazios no JSON mantêm o valor atual da NVS (permite alterar só o Wi-Fi sem reescrever a password do MQTT).

API Key não é configurável pela app Android. É gerida pelo ESP32 (NVS) e pelo servidor (rotação de chaves).

Keepalive BLE a cada 10 segundos, para a conexão aguentar os 5 minutos do modo configuração.

O status=133 (GATT_ERROR) na app Android é cosmético: a operação funciona (o ESP32 recebe, guarda e reinicia), mas o Android não recebe a confirmação GATT a tempo.

## App Android

Projeto: AppWeatherSolutionConfig, Kotlin + Jetpack Compose, minSdk 26.

Ficheiros: MainActivity, ConfigData, BleScanner, BleConfigClient, ui/ScanScreen, ui/ConfigFormScreen.

Permissões: BLUETOOTH_SCAN + BLUETOOTH_CONNECT (Android 12+), BLUETOOTH + BLUETOOTH_ADMIN + ACCESS_FINE_LOCATION (Android 11-).

UUIDs: serviço A001, config A002 (write), log A003 (notify), cancel A004 (write).

Fluxo: scan → escolher dispositivo → conectar → receber logs com dados atuais → pré-preencher formulário → enviar JSON ou cancelar.

Limitação: status=133 cosmético.

## Problemas conhecidos e soluções

| Problema | Solução aplicada |
|---|---|
| Nomes de tabelas em minúsculas no Linux | ToTable("...") em minúsculas no WeatherDbContext |
| Renegociação TLS no Kestrel bloqueava ESP32 | ClientCertificateMode.NoCertificate + endpoint HTTPS no Program.cs |
| MQTTnet 5.x com breaking changes | Downgrade para 4.3.7.1207 |
| API sem ligar ao Mosquitto (NotAuthorized) | Adicionar utilizador WeatherApi ao passwd.txt |
| Restauro de backup com foreign keys duplicadas | DROP DATABASE + recriar antes de restaurar |
| Backup com CREATE DATABASE causava erro de permissões | Remover linhas com sed; usar --no-tablespaces |
| DuckDNS não atualizava IP | Script com verificação OK/KO |
| Visual Studio bloqueava push | Commit novo pela interface Git Changes força reautenticação |
| ESP32 preso em loop de Wi-Fi com SSID errado (ydff/fdd) | Apagar NVS (Tools → Erase Flash → All Flash Contents) |
| MQTT rc=5 após configurar Wi-Fi | Password do MQTT ausente no JSON; app não enviava o campo, ou passwd.txt com permissões erradas |
| Botão de configuração não respondia durante tentativa de Wi-Fi | Verificar o botão dentro do ligarWiFi() a cada 100 ms |
| Conexão BLE caía antes dos 5 minutos | Keepalive BLE a cada 10 s; Wi-Fi desligado no modo configuração |
| status=133 na app Android | Cosmético; operação funciona. Causa provável: coexistência de rádio no ESP32 |

## Fluxo de trabalho Git

Branches: main (produção), dev (integração), feature/* (desenvolvimento).

Fluxo: feature/* → PR → dev → (testar no servidor) → PR → main.

Servidor corre a branch dev. Atualização: git pull origin dev + sudo docker compose up -d --build api webapp (se código mudou).

No PC, o push faz-se pela interface Git Changes do Visual Studio (autenticação gerida pelo IDE). Se bloquear, fazer um commit novo e push.

## Documentação existente

Em docs/: README.md, arquitetura.md, base-de-dados.md, api.md, webapp.md, mqtt.md, esp32.md, deploy.md, backup-restore.md, historico.md, app-android.md.

## Tarefas pendentes

| Item | Prioridade |
|---|---|
| Substituir DHT11 avariado (USE_SIMULATED_READING = false) | Média |
| Configurar setCACert no ESP32 (em vez de setInsecure) | Média |
| Resolver o status=133 na app Android (coexistência de rádio) | Baixa |
| MQTT sobre TLS (porta 8883) | Baixa |
| Backup remoto (S3 ou outro servidor) | Baixa |
| ACLs no Mosquitto | Baixa |
| UPS para o servidor | Baixa |

## Como pedir ajuda numa nova conversa

Cola este resumo no início, e diz explicitamente: o que queres fazer, o que já tentaste, que ficheiros/logs relevantes tens. Se algo já funcionava antes e deixou de funcionar, diz como estava a funcionar, para evitar reconfigurações desnecessárias.