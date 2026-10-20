# Base de Dados

Este documento descreve o modelo de dados do WeatherSolution: entidades, schema, migrations, e as decisões de modelação.

## 1. Visão geral

A base de dados WeatherDb corre em MySQL 8.0. É gerida pelo Entity Framework Core 9 com o provider Pomelo. O schema é versionado por migrations.

A base tem cinco tabelas principais: Users (utilizadores internos e externos), Devices (dispositivos ESP32 registados), DeviceReadings (leituras enviadas por cada dispositivo, por dia), TemperatureReadings (média agregada do dia, consultada pelos utilizadores) e UserQueries (auditoria das consultas efetuadas). Existe ainda a tabela interna __efmigrationshistory, gerida automaticamente pelo EF Core.

## 2. Entidades

### User

| Coluna | Tipo | Descrição |
|---|---|---|
| Id | INT (PK, auto) | Identificador |
| Username | VARCHAR(255), UNIQUE | Nome de utilizador |
| PasswordHash | LONGTEXT | Hash BCrypt da password |
| Role | LONGTEXT | User ou Admin |
| UserType | LONGTEXT | Internal ou External |
| CreatedAt | DATETIME(6) | Data de criação |

O campo UserType distingue utilizadores internos (app web) de externos (API pública). O login interno rejeita utilizadores com UserType = External; o login externo rejeita utilizadores com UserType = Internal.

### Device

| Coluna | Tipo | Descrição |
|---|---|---|
| Id | INT (PK, auto) | Identificador |
| Name | VARCHAR(255) | Nome descritivo |
| Location | LONGTEXT, NULL | Localização física |
| ApiKeyHash | LONGTEXT | Hash BCrypt da API Key |
| IsActive | TINYINT(1) | Estado do dispositivo |
| CreatedAt | DATETIME(6) | Data de criação |
| LastSeenAt | DATETIME(6), NULL | Última comunicação |
| KeyRotationPending | TINYINT(1) | Indica rotação de chave pendente |
| PendingApiKeyHash | LONGTEXT, NULL | Hash da chave pendente |
| PendingApiKeyEncrypted | LONGBLOB, NULL | Chave pendente cifrada (AES) |
| PendingApiKeyIv | LONGBLOB, NULL | Vetor de inicialização da cifra |

A API Key nunca é guardada em claro. É guardada como hash BCrypt, e a versão cifrada (AES) só existe durante a rotação de chave, para ser entregue ao dispositivo via polling.

### DeviceReading

| Coluna | Tipo | Descrição |
|---|---|---|
| Id | INT (PK, auto) | Identificador |
| DeviceId | INT (FK) | Dispositivo que enviou |
| ReadingDate | DATE | Data a que se refere a leitura |
| AverageTemperature | DECIMAL(65,30) | Valor médio enviado |
| ReceivedAt | DATETIME(6) | Quando foi recebido |

Existe um índice único em (DeviceId, ReadingDate). Cada dispositivo envia uma leitura por dia; se enviar outra para o mesmo dia, substitui a anterior.

### TemperatureReading

| Coluna | Tipo | Descrição |
|---|---|---|
| Id | INT (PK, auto) | Identificador |
| ReadingDate | DATE, UNIQUE | Data da leitura |
| AverageTemperature | DECIMAL(65,30) | Média agregada |
| CreatedAt | DATETIME(6) | Data de criação |

Esta tabela é materializada. Não é escrita diretamente pelos dispositivos. É recalculada sempre que um dispositivo envia uma leitura, a partir das leituras dos dispositivos ativos para essa data.

### UserQuery

| Coluna | Tipo | Descrição |
|---|---|---|
| Id | INT (PK, auto) | Identificador |
| UserId | INT (FK) | Utilizador que consultou |
| QueryDate | DATETIME(6) | Quando consultou |
| Endpoint | LONGTEXT | Endpoint consultado |
| TargetDate | DATE | Data alvo da consulta |

Regista todas as consultas efetuadas, incluindo as que devolveram 404 (sem leitura). É usada para auditoria no dashboard do admin.

## 3. Relações

| Relação | Tipo | Comportamento |
|---|---|---|
| User → UserQuery | 1:N | Cascade delete |
| Device → DeviceReading | 1:N | Cascade delete |

Quando um utilizador ou um dispositivo é apagado, as suas queries e leituras são apagadas em cascata.

## 4. Migrations

As migrations estão na pasta WeatherApi/Migrations/, por ordem cronológica:

| Migration | O que faz |
|---|---|
| InitialCreate | Cria Users, TemperatureReadings, UserQueries |
| AddDevices | Cria Devices e DeviceReadings |
| DeviceApiKeyRotation | Adiciona campos de rotação de chave ao Device |
| AddUserType | Adiciona UserType ao User |

A tabela __efmigrationshistory regista quais foram aplicadas. Nunca deve ser apagada manualmente.

## 5. Decisão: nomes de tabelas em minúsculas

O EF Core, por convenção, cria tabelas em PascalCase (Users, Devices, etc.). No entanto, o MySQL no Windows (por defeito) converte os nomes para minúsculas. Quando o backup foi restaurado no Linux, as tabelas estavam em minúsculas (users, devices, etc.), e o EF Core não as encontrava.

A solução foi forçar o EF Core a usar nomes em minúsculas, no OnModelCreating do WeatherDbContext:

```csharp
modelBuilder.Entity<User>().ToTable("users");
modelBuilder.Entity<Device>().ToTable("devices");
modelBuilder.Entity<DeviceReading>().ToTable("devicereadings");
modelBuilder.Entity<TemperatureReading>().ToTable("temperaturereadings");
modelBuilder.Entity<UserQuery>().ToTable("userqueries");
```
Isto garante compatibilidade entre Windows (que força minúsculas) e Linux (que distingue maiúsculas de minúsculas).


## 6. Decisão: TemperatureReadings materializada

Há duas formas de calcular a média que os utilizadores veem: calcular on-the-fly, agregando DeviceReadings em cada consulta, ou materializar numa tabela (TemperatureReadings), recalculada quando um dispositivo envia leitura.

Foi escolhida a opção materializada, porque as consultas dos utilizadores são mais rápidas (um SELECT simples em vez de uma agregação), a agregação só acontece quando um dispositivo envia leitura (uma vez por dia por dispositivo), e o histórico de TemperatureReadings permanece intacto mesmo que um dispositivo seja eliminado.

## 7. Decisão: idempotência em DeviceReading

Cada dispositivo só pode ter uma leitura por dia (índice único em DeviceId + ReadingDate). Se enviar outra para o mesmo dia, a API substitui o valor em vez de criar uma linha duplicada. Isto protege contra reenvios acidentais e mantém a base consistente.

## 8. Datas e fusos horários

Todas as datas são guardadas em UTC. O DateTime.UtcNow no .NET preenche os campos CreatedAt, ReceivedAt, QueryDate e LastSeenAt. O ReadingDate é um DateOnly, que representa o dia a que a leitura se refere (calculado pelo dispositivo em UTC). A conversão para hora local acontece apenas na UI da WebApp, na camada de apresentação.


## 9. Seed de desenvolvimento

O DatabaseSeeder cria dados de teste quando a API arranca em ambiente Development: utilizadores admin (Admin/Internal), joao (User/Internal) e agente01 (User/External); dispositivos Sensor Jardim, Sensor Estufa e Sensor Garagem; leituras dos últimos 3 dias para os dispositivos ativos; agregação das médias diárias.

Em Production, o seed não corre. Só é criado o admin, se não existir.

## 10. Backup e restauro

O processo está documentado em backup-restore.md. O backup é feito com mysqldump, dentro do contentor do MySQL, e é agendado por cron (diariamente às 03:00). A retenção é de 30 dias.