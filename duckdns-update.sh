#!/bin/bash
# DuckDNS updater com verificação de resposta

DOMAIN="ritoluis"
TOKEN="0e91e8d3-5593-4414-adfb-c5bdfa99d27d"

# Obtém o IP público atual
CURRENT_IP=$(curl -4 -s https://api.ipify.org)

# Chama a API do DuckDNS
RESPONSE=$(curl -s "https://www.duckdns.org/update?domains=${DOMAIN}&token=${TOKEN}&ip=${CURRENT_IP}")

# Verifica a resposta
if [ "$RESPONSE" = "OK" ]; then
    echo "$(date '+%Y-%m-%d %H:%M:%S') - IP atualizado para $CURRENT_IP (OK)"
else
    echo "$(date '+%Y-%m-%d %H:%M:%S') - ERRO ao atualizar: resposta='$RESPONSE'" >&2
    exit 1
fi
