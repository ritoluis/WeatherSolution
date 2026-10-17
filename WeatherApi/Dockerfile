# ============================================================
#  Estágio 1: build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copia o .csproj e restaura dependências (cache de camadas)
COPY WeatherApi.csproj .
RUN dotnet restore

# Copia o resto do código e publica
COPY . .
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ============================================================
#  Estágio 2: runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Utilizador não-root (boa prática de segurança)
RUN adduser --disabled-password --gecos "" appuser && chown -R appuser /app
USER appuser

COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "WeatherApi.dll"]
