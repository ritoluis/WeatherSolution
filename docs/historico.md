# Histórico de Desenvolvimento

Este documento regista as decisões tomadas, os problemas encontrados, e as soluções aplicadas ao longo do desenvolvimento do WeatherSolution.

## 1. Fase inicial — Definição da arquitetura

### Decisão: API separada da WebApp

Optou-se por separar a API da aplicação web, em vez de um monolito. Isto permite que agentes externos consumam os mesmos dados, sem estarem acoplados à interface.

### Decisão: Clean Architecture + SOLID

A solução segue Clean Architecture, com camadas bem definidas (Entities, Data, Repositories, Services, Controllers). Os princípios SOLID são aplicados em todo o código, com interfaces para todas as dependências.

### Decisão: MySQL

Escolheu-se MySQL por ser gratuito, amplamente usado, e ter bom suporte em Docker (via Pomelo.EntityFrameworkCore.MySql).

## 2. Autenticação

### Decisão: JWT para utilizadores, API Key para dispositivos

Os utilizadores autenticam-se com JWT. Os dispositivos (ESP32) autenticam-se com API Key, porque não são pessoas e não fazem login. A API Key é mais simples e adequada para comunicação máquina-a-máquina.

### Problema: utilizadores externos conseguiam entrar na app web

Inicialmente, o login interno aceitava qualquer utilizador com credenciais válidas, incluindo os externos. A solução foi adicionar o campo UserType à entidade User, e validar no login: o login interno rejeita UserType=External, e o login externo rejeita UserType=Internal.

## 3. Base de dados

### Problema: nomes de tabelas em minúsculas

O EF Core cria tabelas em PascalCase (Users, Devices, etc.). Mas o MySQL no Windows, por defeito, converte os nomes para minúsculas. Quando o backup foi restaurado no Linux (que distingue maiúsculas de minúsculas), o EF Core não encontrava as tabelas.

A solução foi forçar o EF Core a usar nomes em minúsculas, no OnModelCreating, com ToTable("users"), ToTable("devices"), etc.

### Problema: conflito de foreign keys no restauro

Ao restaurar o backup no servidor, o MySQL recusou a criação da constraint FK_DeviceReadings_Devices_DeviceId, porque já existia (de uma tentativa anterior). A solução foi apagar a base de dados inteira e recriá-la antes de restaurar.

### Problema: backup com CREATE DATABASE causava erro de permissões

O ficheiro de backup gerado com mysqldump --databases incluía CREATE DATABASE e USE, que o utilizador WeatherSolution não tinha permissão para executar. A solução foi remover essas linhas do ficheiro com sed.

### Decisão: TemperatureReadings materializada

A média que os utilizadores veem é calculada a partir das leituras dos dispositivos. Optou-se por materializar o resultado numa tabela (TemperatureReadings), recalculada quando um dispositivo envia leitura, em vez de calcular on-the-fly em cada consulta.

## 4. API

### Problema: renegociação TLS no Kestrel

O ESP32 não conseguia completar o handshake TLS com a API, porque o Kestrel pedia renegociação TLS (client certificate). A solução foi configurar o Kestrel com ClientCertificateMode.NoCertificate, e definir o endpoint HTTPS diretamente no Program.cs, em vez de o deixar vir do launchSettings.json.

### Decisão: Swagger apenas em desenvolvimento

O Swagger é útil em desenvolvimento, mas não deve estar exposto em produção. A configuração ficou condicionada ao ambiente.

## 5. MQTT

### Decisão: MQTT em vez de polling

Para a leitura sob pedido (quando o admin clica em Ler Agora), optou-se por MQTT em vez de polling HTTP. O MQTT permite comunicação bidirecional eficiente, sem desperdício de pedidos.

### Problema: MQTTnet 5.x com breaking changes

A versão 5.x da MQTTnet tem breaking changes em relação à 4.x. A solução foi fazer downgrade para a versão 4.3.7.1207, que é estável e compatível com o código escrito.

### Problema: API não se ligava ao Mosquitto (NotAuthorized)

A API recebia NotAuthorized ao tentar ligar-se ao Mosquitto. A causa era a falta do utilizador WeatherApi no ficheiro passwd.txt (só o SensorJardim existia). A solução foi adicionar o WeatherApi com mosquitto_passwd -b.

### Problema: Mosquitto não arrancava como serviço no Windows

O serviço do Windows falhava com erro 1053. A causa era o caminho relativo do password_file no mosquitto.conf. A solução foi usar o caminho absoluto e dar permissões ao SYSTEM.

### Decisão: rotação de chave com polling

Em vez de o admin ter de injetar a nova chave manualmente no dispositivo, implementou-se um mecanismo de polling: o dispositivo consulta /api/device/config uma vez por dia, e adota a nova chave se houver uma pendente.

## 6. ESP32

### Problema: DHT11 avariado

O sensor DHT11 não respondia (falha ao ler). Testaram-se dois sensores e dois GPIOs, sem sucesso. A solução temporária foi implementar um modo simulado (USE_SIMULATED_READING), que gera valores aleatórios em vez de ler o sensor. Quando o sensor novo chegar, basta mudar o flag para false.

### Problema: HTTPS no ESP32 falhava

O ESP32 não conseguia ligar-se à API por HTTPS. A causa era a renegociação TLS no Kestrel (já resolvida). A solução foi também configurar o ESP32 com setInsecure() (aceita qualquer certificado), adequado para desenvolvimento.

### Decisão: NTP em UTC

O ESP32 sincroniza a hora em UTC, sem aplicar o fuso horário de Portugal. A conversão para hora local acontece na UI da WebApp.

## 7. Deploy

### Decisão: Docker Compose

Todos os serviços correm em contentores Docker, orquestrados por Docker Compose. Isto garante reprodutibilidade e isolamento.

### Decisão: servidor Debian 13 sem interface gráfica

O servidor é Debian 13 (Trixie), sem interface gráfica, gerido por SSH. A escolha prende-se com a poupança de recursos (4 GB de RAM) e a segurança.

### Problema: certificado Let's Encrypt

A emissão do certificado correu bem, mas a renovação automática exigia um cron no host (o contentor certbot não recarrega o Nginx). A solução foi criar o script renew-cert.sh e agendá-lo no cron.

### Problema: DuckDNS não atualizava o IP

O script original do DuckDNS usava curl -fsS, que não falha quando o DuckDNS devolve KO. O script parecia funcionar, mas o IP não era atualizado. A solução foi criar o duckdns-update.sh, que verifica a resposta OK/KO e regista no log.

### Decisão: backup automático com cron

O backup da base de dados é feito por cron (diariamente às 03:00), com retenção de 30 dias. O script usa mysqldump --no-tablespaces (para evitar o erro de permissões do PROCESS).

## 8. Estado final

| Componente | Estado |
|---|---|
| API com JWT + API Key | Funcional |
| WebApp com login interno | Funcional |
| Dashboard de temperatura | Funcional |
| Auditoria de consultas | Funcional |
| Gestão de dispositivos | Funcional |
| MQTT (leitura sob pedido) | Funcional |
| ESP32 (modo simulado) | Funcional |
| HTTPS com Let's Encrypt | Funcional |
| Renovação automática do certificado | Configurada |
| Backup automático da base de dados | Configurado |
| DuckDNS com verificação | Configurado |
| Port forwarding | Configurado |

## 9. Próximos passos (pendentes)

| Item | Prioridade |
|---|---|
| Substituir o DHT11 avariado | Média |
| Configurar setCACert no ESP32 (em vez de setInsecure) | Média |
| Configurar MQTT sobre TLS (porta 8883) | Baixa |
| Backup remoto (S3 ou outro servidor) | Baixa |
| UPS para o servidor | Baixa |
| ACLs no Mosquitto | Baixa |