# WeatherSolution — Documentação do Projeto

Sistema de recolha e consulta de temperaturas médias diárias, com API REST, aplicação web MVC, broker MQTT e dispositivos ESP32.

## Visão Geral

O WeatherSolution é composto por três aplicações que trabalham em conjunto:

| Aplicação | Tipo | Função |
|---|---|---|
| **WeatherApi** | ASP.NET Core Web API | Endpoints REST, autenticação, acesso à base de dados, integração MQTT |
| **WeatherWebApp** | ASP.NET Core MVC | Interface web para utilizadores internos e administradores |
| **WeatherExternalClient** | Consola .NET | Simulador de agente externo, para testar a API pública |

Além destas, há um **dispositivo ESP32** que envia leituras de temperatura para a API e responde a pedidos de leitura em tempo real via MQTT.

## Como o sistema funciona

1. O **ESP32** lê a temperatura de hora a hora e acumula as leituras.
2. Às **00:05 do dia seguinte**, calcula a média do dia anterior e envia-a à **API**, autenticado com uma **API Key**.
3. A API guarda a leitura e **recalcula a média agregada** do dia, considerando todos os dispositivos ativos.
4. Os **utilizadores internos** (app web) e os **agentes externos** (API pública) consultam a média do dia anterior.
5. O **admin** consulta todas as datas, a auditoria das consultas, e gere os dispositivos.
6. O admin pode pedir uma **leitura imediata** ao dispositivo via **MQTT**, sem esperar pelo ciclo diário.

## Estrutura da documentação

| Documento | Conteúdo |
|---|---|
| [arquitetura.md](arquitetura.md) | Clean Architecture, SOLID, injeção de dependências, camadas |
| [base-de-dados.md](base-de-dados.md) | Entidades, schema, migrations, decisões de modelação |
| [api.md](api.md) | Endpoints, autenticação JWT, API Keys, policies, rate limiting |
| [webapp.md](webapp.md) | MVC, views Razor, fluxo de login, dashboard do admin |
| [mqtt.md](mqtt.md) | Broker Mosquitto, tópicos, rotação de chaves, leitura sob pedido |
| [esp32.md](esp32.md) | Hardware, firmware, NTP, envio diário, modo simulado |
| [deploy.md](deploy.md) | Docker, Nginx, Certbot, DuckDNS, port forwarding, cron |
| [backup-restore.md](backup-restore.md) | Backup e restauro da base de dados (Windows e Linux) |
| [historico.md](historico.md) | Decisões, problemas e soluções ao longo do desenvolvimento |

## Stack tecnológico

| Camada | Tecnologia |
|---|---|
| Plataforma | .NET 9.0 |
| API | ASP.NET Core Web API |
| Web | ASP.NET Core MVC + Razor + Bootstrap 5 |
| Base de dados | MySQL 8.0 |
| ORM | Entity Framework Core 9 + Pomelo |
| Autenticação | JWT (utilizadores) + API Key (dispositivos) |
| Broker MQTT | Eclipse Mosquitto 2.0 |
| Cliente MQTT | MQTTnet 4.3 |
| Dispositivo | ESP32 WROOM-32D + DHT11 |
| Reverse proxy | Nginx |
| HTTPS | Let's Encrypt (Certbot) |
| Contentorização | Docker + Docker Compose |
| Servidor | Debian 13 (Trixie) |
| DDNS | DuckDNS |
| Controlo de versões | Git + GitHub |

## Modelo de permissões

| Ator | Ver dia anterior | Ver várias datas | Ver auditoria | Gerir dispositivos |
|---|---|---|---|---|
| Agente externo (`client_type=external`) | ✅ | ❌ | ❌ | ❌ |
| Utilizador interno (`client_type=internal`, role `User`) | ✅ | ✅ | ❌ | ❌ |
| Admin (`client_type=internal`, role `Admin`) | ✅ | ✅ | ✅ | ✅ |
| Dispositivo (API Key) | — | — | — | — (só envia leituras) |

## Repositório

- **GitHub**: `https://github.com/ritoluis/WeatherSolution`
- **Branches**: `main` (produção), `dev` (integração), `feature/*` (desenvolvimento)
- **Fluxo**: `feature/*` → `dev` → (testar no servidor) → `main`

## Estado atual

O sistema está em produção num servidor caseiro (Debian 13), acessível via HTTPS em `https://ritoluis.duckdns.org`. Tem renovação automática de certificado, backup automático da base de dados, e atualização automática do DDNS.