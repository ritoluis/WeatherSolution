# Aplicação Web (MVC)

Este documento descreve a aplicação web WeatherWebApp: o fluxo de login, as views Razor, o dashboard do admin, e as decisões de apresentação.

## 1. Visão geral

A WeatherWebApp é uma aplicação ASP.NET Core MVC, que serve de interface para utilizadores internos e administradores. Não acede diretamente à base de dados: toda a comunicação passa pela API, via HttpClient.

A aplicação usa sessão HTTP para guardar o token JWT após o login. As views são Razor, com Bootstrap 5 para estilização.

## 2. Estrutura de pastas

| Pasta | Conteúdo |
|---|---|
| Controllers/ | AccountController, TemperatureController, AdminController, AdminDevicesController |
| Models/ | ViewModels (LoginViewModel, TemperatureViewModel, DeviceListViewModel, etc.) |
| Services/ | ApiClient, JwtHelper |
| Views/ | Razor Views, organizadas por controller |
| Extensions/ | DateTimeExtensions (conversão UTC → local) |
| wwwroot/ | Ficheiros estáticos (CSS, JS, Bootstrap, jQuery) |

## 3. Fluxo de login

1. O utilizador acede a /Account/Login e introduz username e password.
2. O AccountController chama o ApiClient, que faz POST para /api/auth/login/internal.
3. A API valida as credenciais e devolve um token JWT.
4. O controller guarda o token na sessão HTTP, juntamente com o username e a role.
5. A role é lida diretamente do token (claim role), não inferida pelo username.
6. O utilizador é redirecionado para /Temperature/Index.

Se o login falhar, o controller mostra uma mensagem de erro na view.

## 4. Sessão

A sessão HTTP guarda três valores:

| Chave | Conteúdo |
|---|---|
| Token | Token JWT |
| Username | Nome do utilizador |
| Role | User ou Admin |

A sessão é configurada no Program.cs com AddSession e UseSession. O token é enviado em cada pedido à API, no header Authorization: Bearer.

## 5. Layout e navegação

O _Layout.cshtml define a estrutura comum a todas as páginas:

- Navbar com o nome da aplicação (link para /Temperature/Index).
- Se o utilizador estiver autenticado: mostra o username, um badge Admin (se aplicável), e os botões de navegação.
- Se for admin: mostra os links Utilizadores e Dispositivos.
- Botão Sair, que limpa a sessão e redireciona para o login.
- Navbar responsiva, com botão hambúrguer em ecrãs pequenos.

## 6. Consulta de temperatura

A view /Temperature/Index é acessível a qualquer utilizador autenticado.

- Mostra um formulário com um date picker e os botões Consultar e Dia Anterior.
- Se o utilizador escolher uma data, consulta essa data.
- Se não escolher, mostra o dia anterior por defeito.
- Se a API devolver 404 (sem leitura), mostra a mensagem devolvida pela API.
- Se for admin, mostra também a tabela de auditoria (consultas efetuadas).

## 7. Dashboard do admin

O admin tem acesso a três áreas adicionais:

### Auditoria

Na própria página /Temperature/Index, o admin vê a tabela de consultas efetuadas por todos os utilizadores, com username, role, data da consulta, endpoint e data alvo.

### Gestão de utilizadores

Em /Admin/Users, o admin vê a lista de utilizadores e pode criar novos (internos ou externos).

### Gestão de dispositivos

Em /AdminDevices/Index, o admin vê a lista de dispositivos com nome, localização, estado, última leitura e última comunicação. Pode:

- Criar um novo dispositivo (a API Key é mostrada uma vez).
- Ativar ou desativar um dispositivo.
- Eliminar um dispositivo.
- Pedir uma leitura imediata via MQTT (botão Ler Agora).
- Ver os detalhes de um dispositivo, com histórico de leituras.

## 8. Conversão UTC → hora local

Todas as datas na API estão em UTC. A conversão para hora local acontece na camada de apresentação, através da extensão ToLocalString, definida em Extensions/DateTimeExtensions.cs.

A extensão trata de DateTime e DateTime?. Quando o valor é nulo, devolve a string fornecida (por exemplo, nunca comunicou).

## 9. Formatação de temperaturas

As temperaturas são do tipo decimal, que pode ter muitas casas decimais. Para evitar mostrar valores como 18.580000000000000, os ViewModels expõem propriedades formatadas:

| ViewModel | Propriedade | Formato |
|---|---|---|
| TemperatureViewModel | AverageTemperatureDisplay | 0.00 (InvariantCulture) |
| DeviceListViewModel | LastReadingTemperatureDisplay | 0.00 |
| DeviceReadingViewModel | AverageTemperatureDisplay | 0.00 |
| RequestReadingResultViewModel | TemperatureDisplay | 0.00 |

A formatação usa CultureInfo.InvariantCulture para garantir o ponto como separador decimal, independentemente do sistema.

## 10. Tratamento de erros da API

O ApiClient distingue três situações:

| Situação | Comportamento |
|---|---|
| 200 OK | Devolve o ViewModel preenchido |
| 404 Not Found | Devolve um ViewModel com a Message preenchida (sem leitura) |
| Outro erro | Devolve null; o controller mostra uma mensagem genérica |

Isto permite que a view distinga entre sem leitura (mensagem específica da API) e erro de comunicação.

## 11. Responsividade

A aplicação usa Bootstrap 5, que é responsivo por defeito. Foram feitos ajustes pontuais:

- Navbar com botão hambúrguer em ecrãs pequenos.
- Tabelas envolvidas em div class="table-responsive" para scroll horizontal.
- Formulários com colunas responsivas (col-12 col-sm-auto).
- Botões de ação com flex-wrap para quebrarem em ecrãs pequenos.

## 12. Ficheiros principais

| Ficheiro | Função |
|---|---|
| Controllers/AccountController.cs | Login, logout, acesso negado |
| Controllers/TemperatureController.cs | Consulta de temperatura |
| Controllers/AdminController.cs | Gestão de utilizadores |
| Controllers/AdminDevicesController.cs | Gestão de dispositivos |
| Services/ApiClient.cs | Comunicação com a API |
| Services/JwtHelper.cs | Leitura de claims do token |
| Extensions/DateTimeExtensions.cs | Conversão UTC → local |
| Views/Shared/_Layout.cshtml | Layout comum |
| Views/Temperature/Index.cshtml | Página de consulta |
| Views/AdminDevices/Index.cshtml | Lista de dispositivos |
| Views/AdminDevices/Details.cshtml | Detalhes do dispositivo |