# App Android (Configuração BLE)

Este documento descreve a app Android de configuração do ESP32 via Bluetooth Low Energy (BLE): o que faz, como está estruturada, e como se liga ao dispositivo.

## 1. Visão geral

A app Android permite configurar o ESP32 sem ser preciso ligá-lo a um PC. O utilizador:

1. Pressiona o botão de configuração no ESP32.
2. Abre a app no telemóvel.
3. A app procura dispositivos BLE com o nome PROV-Sensor*.
4. O utilizador escolhe o dispositivo.
5. A app liga-se ao ESP32 e recebe os dados atuais (SSID, MQTT, API, Device ID).
6. O utilizador altera o que precisar e envia a configuração.
7. O ESP32 guarda na NVS e reinicia.

A app foi escrita em Kotlin, com Jetpack Compose para a interface.

## 2. Estrutura do projeto

| Ficheiro | Função |
|---|---|
| MainActivity.kt | Ponto de entrada, navegação entre ecrãs |
| ConfigData.kt | Modelo do JSON enviado ao ESP32 |
| BleScanner.kt | Scan de dispositivos BLE, filtro por nome |
| BleConfigClient.kt | Conexão, descoberta GATT, escrita, notificações |
| ui/ScanScreen.kt | Ecrã de scan (lista de dispositivos) |
| ui/ConfigFormScreen.kt | Ecrã de configuração (formulário) |

## 3. Permissões

A app pede permissões de BLE em runtime, com diferenças entre versões do Android:

| Versão | Permissões |
|---|---|
| Android 12+ (API 31+) | BLUETOOTH_SCAN, BLUETOOTH_CONNECT |
| Android 11 e anteriores | BLUETOOTH, BLUETOOTH_ADMIN, ACCESS_FINE_LOCATION |

A permissão BLUETOOTH_SCAN tem o atributo neverForLocation, que diz ao Android que a app não usa o BLE para inferir localização. Isto permite pedir a permissão de scan sem a permissão de localização em Android 12+.

O minSdk do projeto é 26 (Android 8.0).

## 4. UUIDs do serviço e características

| UUID | Tipo | Função |
|---|---|---|
| 0000A001-0000-1000-8000-00805F9B34FB | Serviço | Serviço de provisionamento |
| 0000A002-0000-1000-8000-00805F9B34FB | Característica (Write) | Recebe o JSON de configuração |
| 0000A003-0000-1000-8000-00805F9B34FB | Característica (Notify) | Recebe logs do ESP32 |
| 0000A004-0000-1000-8000-00805F9B34FB | Característica (Write) | Recebe o comando de cancelar |

Os UUIDs são os mesmos que estão definidos no sketch do ESP32.

## 5. Fluxo de utilização

1. O ScanScreen pede as permissões de BLE e inicia o scan.
2. O BleScanner filtra apenas dispositivos cujo nome começa por PROV-Sensor.
3. O utilizador escolhe um dispositivo. O MainActivity cria um BleConfigClient e navega para o ConfigFormScreen.
4. O BleConfigClient liga-se ao dispositivo e descobre os serviços.
5. Ao descobrir o serviço A001, ativa as notificações na característica de log (A003) e marca o estado como READY.
6. O ESP32 envia os dados atuais via logs (SSID, MQTT server/port/user, API host/port, Device ID).
7. O ConfigFormScreen pré-preenche os campos com os valores dos logs.
8. O utilizador altera o que precisar e clica em Enviar.
9. A app envia o JSON na característica A002.
10. O ESP32 guarda na NVS, envia um log de confirmação, e reinicia.
11. O utilizador clica em Cancelar envia o comando cancel na característica A004, e o ESP32 reinicia sem alterar nada.

## 6. Modelo do JSON

O ConfigData.toJson() produz este JSON:

{
  "wifi": {
    "ssid": "...",
    "password": "..."
  },
  "mqtt": {
    "server": "...",
    "port": 1883,
    "user": "...",
    "password": "..."
  },
  "api": {
    "host": "...",
    "port": 443
  },
  "deviceId": 1
}

Só wifi.ssid é obrigatório. Os restantes campos, se vierem vazios ou ausentes no JSON, mantêm o valor atual da NVS no ESP32.

## 7. O que a app NÃO faz

A app não configura a API Key. A API Key é gerada pelo servidor (dashboard) e gerida pelo ESP32 (NVS, chave apiKey) e pelo mecanismo de rotação de chaves (/api/device/config + /api/device/confirm-key). A app não a vê, não a envia, e não a altera.

A app também não envia o Device ID se o utilizador não o alterar. O Device ID vem da NVS do ESP32 e é enviado nos logs.

## 8. Pré-preenchimento dos campos

O ConfigFormScreen observa os logs recebidos do ESP32 e pré-preenche os campos com base nas linhas:

INFO: SSID atual: <ssid>
INFO: MQTT server: <server>
INFO: MQTT port: <port>
INFO: MQTT user: <user>
INFO: API host: <host>
INFO: API port: <port>
INFO: Device ID: <id>

A password do MQTT não é enviada nos logs (por segurança). O campo fica vazio, e o utilizador pode deixá-lo vazio (o ESP32 mantém a password atual) ou escrever uma nova.

## 9. Limitações conhecidas

O status=133 (GATT_ERROR) aparece na app quando o utilizador envia o JSON ou cancela. É um erro cosmético: a operação funciona na mesma (o ESP32 recebe, guarda e reinicia), mas o Android não recebe a confirmação GATT a tempo, porque o ESP32 reinicia imediatamente após a escrita.

A causa provável é a coexistência de rádio no ESP32: o Wi-Fi e o BLE partilham o mesmo módulo de 2.4 GHz, e o Wi-Fi pode "abafar" o BLE durante a escrita. A solução definitiva (dar prioridade ao BLE via API de coexistência) não está disponível no core Arduino-ESP32 usado no projeto.

## 10. Como compilar e instalar

1. Abrir o projeto no Android Studio.
2. Confirmar que o minSdk é 26.
3. Ativar a Depuração USB no telemóvel (Opções de Programador).
4. Ligar o telemóvel ao PC por USB.
5. Aceitar o diálogo "Permitir depuração USB?" no telemóvel.
6. Clicar em Run no Android Studio.