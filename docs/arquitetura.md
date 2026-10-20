# Arquitetura

Este documento descreve a arquitetura do WeatherSolution: como as camadas se organizam, que princípios seguem, e porque foram tomadas as decisões estruturais.

## 1. Visão geral

A solução segue os princípios da Clean Architecture e do MVC, com separação rigorosa de responsabilidades. Cada projeto tem uma função distinta, e dentro de cada projeto o código está organizado por camadas lógicas.

O Browser (utilizador interno), os agentes externos e o ESP32 comunicam com a API por HTTP/HTTPS. O ESP32 comunica também com o Mosquitto por MQTT. A API acede ao MySQL.

## 2. Projetos da solução

| Projeto | Tipo | Responsabilidade |
|---|---|---|
| WeatherApi | ASP.NET Core Web API | Endpoints REST, autenticação, acesso a dados, integração MQTT, agregação de temperaturas |
| WeatherWebApp | ASP.NET Core MVC | Interface web para utilizadores internos e admin, consome a API via HttpClient |
| WeatherExternalClient | Consola .NET | Simulador de agente externo, testa a API pública como um parceiro real o faria |

## 3. Camadas dentro da API

| Camada | Pasta | Responsabilidade |
|---|---|---|
| Entities | Entities/ | Modelos de domínio (User, Device, DeviceReading, TemperatureReading, UserQuery) |
| Data | Data/ | WeatherDbContext, configuração do EF Core, seed da base de dados |
| Repositories | Repositories/ | Abstração do acesso a dados (ITemperatureRepository, IUserRepository, IDeviceRepository) |
| Services | Services/ | Lógica de negócio (ITemperatureService, ITokenService, IApiKeyService, ILoginAttemptService, ISecretProtector, IMqttService) |
| Controllers | Controllers/ | Pontos de entrada HTTP, delegam nos serviços |
| DTOs | DTOs/ | Objetos de transferência de dados, definem o contrato público da API |
| Auth | Auth/ | Handler de autenticação por API Key (DeviceKeyAuthenticationHandler) |

Regra de dependência: as dependências apontam sempre para dentro (Controllers → Services → Repositories → Data → Entities). Os controladores nunca acedem diretamente ao DbContext; os serviços nunca conhecem os controladores; os repositórios nunca contêm lógica de negócio.

## 4. Princípios SOLID aplicados

S — Single Responsibility: cada classe tem uma única responsabilidade. TokenService gera tokens JWT; ApiKeyService gera, faz hash e valida API Keys; TemperatureService concentra a lógica de temperaturas e agregação; LoginAttemptService controla tentativas falhadas e bloqueios; SecretProtector cifra e decifra segredos com AES; MqttService gere a ligação ao broker.

O — Open/Closed: o código está aberto a extensão, fechado a modificação, via interfaces. Por exemplo, ITemperatureRepository define o contrato; se amanhã se quiser trocar MySQL por PostgreSQL, cria-se PostgresTemperatureRepository sem alterar o código que a usa.

L — Liskov Substitution: qualquer implementação de uma interface pode substituir outra sem quebrar o sistema.

I — Interface Segregation: interfaces pequenas e focadas. ITokenService tem apenas GenerateToken; ILoginAttemptService tem apenas os métodos de bloqueio; ISecretProtector tem apenas Protect e Unprotect.

D — Dependency Inversion: os controladores dependem de abstrações, não de concreções. Por exemplo, InternalTemperatureController recebe ITemperatureService e IUserRepository no construtor, nunca faz new TemperatureService(...). O container de DI injeta as implementações registadas no Program.cs.

## 5. Injeção de dependências

Todas as dependências são registadas no Program.cs e injetadas via construtor.

| Lifetime | Quando usar | Exemplos no projeto |
|---|---|---|
| Singleton | Uma instância para toda a aplicação | ISecretProtector, IMqttService |
| Scoped | Uma instância por pedido HTTP | WeatherDbContext, repositórios, serviços |
| Transient | Uma instância nova cada vez | (não usado no projeto) |

O MqttService é registado como Singleton e como HostedService, para que a mesma instância seja usada tanto pelos controladores como pelo serviço em segundo plano.

## 6. Camadas dentro da WebApp

| Camada | Pasta | Responsabilidade |
|---|---|---|
| Controllers | Controllers/ | Recebem pedidos HTTP, chamam o ApiClient, devolvem views |
| Models | Models/ | ViewModels (não são entidades de domínio) |
| Services | Services/ | ApiClient — comunica com a API via HttpClient injetado |
| Views | Views/ | Razor Views, organizadas por controller |
| Extensions | Extensions/ | Extensões de DateTime para conversão UTC → local |

A WebApp não acede diretamente à base de dados. Toda a comunicação passa pela API, via HTTP. Isto garante uma única fonte de verdade, concentra a lógica de negócio num só sítio, e permite substituir a WebApp sem afetar a API.

## 7. Autenticação em duas camadas

| Esquema | Para quem | Como funciona |
|---|---|---|
| JWT Bearer | Utilizadores (internos e externos) | Login devolve token; enviado no header Authorization: Bearer <token> |
| API Key | Dispositivos (ESP32) | Chave única por dispositivo, enviada no header X-Device-Key |

Os dois esquemas coexistem no mesmo pipeline de autenticação. Os endpoints indicam qual o esquema a usar: [Authorize] usa JWT (esquema por defeito); [Authorize(AuthenticationSchemes = "DeviceKey")] usa API Key.

## 8. Porque Clean Architecture + MVC

| Decisão | Justificação |
|---|---|
| Clean Architecture | Separação de responsabilidades, testabilidade, independência de frameworks |
| MVC na WebApp | Padrão nativo do ASP.NET Core, adequado a aplicações com formulários e views server-side |
| API REST separada | Permite que agentes externos consumam os mesmos dados, sem acoplar à UI |
| Repositórios | Abstraem o acesso a dados, permitem trocar a base de dados sem alterar a lógica |
| Serviços | Concentram a lógica de negócio, mantêm os controladores finos |
| DTOs | Definem o contrato público, evitam expor entidades internas |

## 9. Fluxo de uma consulta de temperatura

O utilizador escolhe uma data na view. O TemperatureController da WebApp chama o ApiClient, que faz um pedido HTTP autenticado à API. A API valida o token, identifica o utilizador, e chama o TemperatureService. O serviço consulta o repositório, que lê a base de dados. A resposta percorre o caminho inverso até ao browser.

## 10. Resumo das decisões estruturais

| Decisão | Alternativa considerada | Porque foi escolhida |
|---|---|---|
| Clean Architecture | Monolito simples | Testabilidade e separação de responsabilidades |
| MVC + Razor | SPA (React/Angular) | Simplicidade, sem necessidade de frontend separado |
| API REST separada | Lógica na WebApp | Permite agentes externos |
| MQTT | Polling HTTP | Comunicação bidirecional, eficiente, padrão IoT |
| JWT + API Key | Só JWT | Dispositivos não são utilizadores; API Key é mais adequada |
| MySQL | SQL Server / PostgreSQL | Gratuito, amplamente usado, bom suporte em Docker |
| Docker Compose | Instalação manual | Reprodutibilidade e isolamento |