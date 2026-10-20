# API REST

Este documento descreve os endpoints da API, os esquemas de autenticação, as policies de autorização, e as proteções implementadas.

## 1. Visão geral

A API é uma ASP.NET Core Web API, com controllers organizados por área funcional. Todos os endpoints devolvem JSON, exceto os que devolvem ficheiros (nenhum, no estado atual).

A documentação interativa (Swagger) está disponível em `/swagger`, apenas em ambiente de desenvolvimento. Em produção, o Swagger não está exposto.

## 2. Grupos de endpoints

| Grupo | Prefixo | Para quem | Autenticação |
|---|---|---|---|
| Autenticação | /api/auth | Utilizadores e dispositivos | Nenhuma (é o login) |
| API pública | /api/public | Agentes externos | JWT com client_type=external |
| API interna | /api/internal | Utilizadores internos e admin | JWT com client_type=internal |
| Administração | /api/admin | Admin | JWT com role=Admin |
| Dispositivos | /api/device | ESP32 | API Key |

## 3. Autenticação

### Login interno
POST /api/auth/login/internal
Body: { "username": "...", "password": "..." }

text

Devolve um token JWT com client_type=internal. Rejeita utilizadores com UserType=External.

### Login externo
POST /api/auth/login/external
Body: { "username": "...", "password": "..." }

text

Devolve um token JWT com client_type=external. Rejeita utilizadores com UserType=Internal.

### Claims do token

| Claim | Valor |
|---|---|
| nameid | Id do utilizador |
| unique_name | Username |
| role | User ou Admin |
| client_type | internal ou external |
| exp | Data de expiração (UTC) |
| iss | WeatherApi |
| aud | WeatherClients |

### Criação de conta externa
POST /api/auth/create-external
Autorização: Admin
Body: { "username": "...", "password": "..." }

text

Cria um utilizador com UserType=External e role=User.

## 4. API pública

### Consultar temperatura do dia anterior
GET /api/public/temperature/yesterday
Autorização: JWT com client_type=external

text

Devolve 200 com a temperatura, ou 404 com mensagem se não houver leitura. É o único endpoint acessível a agentes externos.

## 5. API interna

### Consultar temperatura do dia anterior
GET /api/internal/temperature/yesterday
Autorização: JWT com client_type=internal

text

### Consultar temperatura de uma data
GET /api/internal/temperature/by-date/{date}
Autorização: JWT com client_type=internal

text

A data é no formato yyyy-MM-dd. Devolve 200 com a temperatura, ou 404 com mensagem se não houver leitura.

### Consultar intervalo de datas
GET /api/internal/temperature/range?from={date}&to={date}
Autorização: JWT com client_type=internal

text

Devolve uma lista de temperaturas no intervalo.

## 6. Administração

### Auditoria de consultas
GET /api/admin/queries?username={username}
Autorização: JWT com role=Admin

text

Devolve todas as consultas efetuadas, com filtro opcional por username.

### Listar utilizadores
GET /api/admin/users
Autorização: JWT com role=Admin

text

### Criar utilizador interno
POST /api/admin/users/internal
Autorização: JWT com role=Admin
Body: { "username": "...", "password": "..." }

text

Cria um utilizador com UserType=Internal e role=User.

### Listar dispositivos
GET /api/admin/devices
Autorização: JWT com role=Admin

text

Devolve a lista de dispositivos com última leitura e última comunicação.

### Criar dispositivo
POST /api/admin/devices
Autorização: JWT com role=Admin
Body: { "name": "...", "location": "..." }

text

Devolve os dados do dispositivo e a API Key em claro. A chave só é mostrada uma vez.

### Detalhes de um dispositivo
GET /api/admin/devices/{id}
Autorização: JWT com role=Admin

text

### Ativar dispositivo
PUT /api/admin/devices/{id}/activate
Autorização: JWT com role=Admin

text

### Desativar dispositivo
PUT /api/admin/devices/{id}/deactivate
Autorização: JWT com role=Admin

text

### Eliminar dispositivo
DELETE /api/admin/devices/{id}
Autorização: JWT com role=Admin

text

Apaga o dispositivo e as suas leituras (cascade).

### Regenerar API Key
POST /api/admin/devices/{id}/regenerate-key
Autorização: JWT com role=Admin

text

Gera uma nova chave, marca KeyRotationPending=true, e devolve a chave em claro uma vez. O dispositivo adota-a no próximo polling.

### Histórico de leituras
GET /api/admin/devices/{id}/readings?from={date}&to={date}&limit={n}
Autorização: JWT com role=Admin

text

### Leitura imediata via MQTT
POST /api/admin/devices/{id}/request-reading
Autorização: JWT com role=Admin

text

Publica read_now no tópico MQTT do dispositivo e espera até 10 segundos pela resposta. Devolve 200 com a temperatura, ou 504 se o dispositivo não responder.

### Média atual
GET /api/admin/temperature/current
Autorização: JWT com role=Admin

text

Devolve a média agregada do dia anterior.

## 7. API dos dispositivos

### Enviar leitura diária
POST /api/device/reading
Autorização: API Key (X-Device-Key)
Body: { "date": "2026-10-20", "averageTemperature": 21.5 }

text

A data é no formato yyyy-MM-dd. A leitura é guardada e a média do dia é recalculada. Se já existir leitura para o mesmo dia, é substituída.

### Consultar configuração
GET /api/device/config
Autorização: API Key (X-Device-Key)

text

Devolve o estado do dispositivo e, se KeyRotationPending=true, a nova chave em claro.

### Confirmar rotação de chave
POST /api/device/confirm-key
Autorização: API Key (X-Device-Key, a nova)

text

Promove a chave pendente a chave atual.

## 8. Policies de autorização

| Policy | Requisito | Aplicada a |
|---|---|---|
| InternalClient | client_type=internal | /api/internal/* |
| ExternalClient | client_type=external | /api/public/* |
| (role Admin) | role=Admin | /api/admin/* e /api/auth/create-external |
| DeviceKey | API Key válida | /api/device/* |

## 9. Rate limiting

Configurado no appsettings.json:

| Endpoint | Limite |
|---|---|
| Todos | 20 pedidos por 10 segundos, por IP |
| /api/auth/* | 5 pedidos por minuto, por IP |
| /api/public/* | 30 pedidos por minuto, por IP |

Quando o limite é excedido, a API devolve 429 Too Many Requests, com headers X-Rate-Limit-*.

## 10. Bloqueio de conta

Após 3 tentativas de login falhadas para o mesmo username, a conta fica bloqueada durante 5 minutos. Durante esse período, mesmo a password correta é rejeitada com 423 Locked.

O controlo é feito em memória (IMemoryCache). Reiniciar a API limpa os bloqueios.

## 11. Códigos de resposta

| Código | Significado |
|---|---|
| 200 | Sucesso |
| 400 | Pedido inválido (dados em falta ou fora do intervalo) |
| 401 | Não autenticado (token em falta ou inválido) |
| 403 | Sem permissão (token válido mas sem acesso) |
| 404 | Recurso não encontrado (ou sem leitura para a data) |
| 409 | Conflito (username já existe) |
| 423 | Conta bloqueada temporariamente |
| 429 | Rate limit excedido |
| 500 | Erro interno |
| 504 | Timeout (dispositivo não respondeu via MQTT) |

## 12. Segurança

- Todos os endpoints (exceto login) exigem autenticação.
- O login interno rejeita utilizadores externos e vice-versa.
- As passwords são guardadas com BCrypt.
- As API Keys são guardadas com BCrypt.
- A rotação de chave usa AES para cifrar a chave pendente.
- O rate limiting protege contra brute force.
- O bloqueio de conta protege contra ataques por username.
- Todos os logs de tentativas falhadas incluem username e IP.