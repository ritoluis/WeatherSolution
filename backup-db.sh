#!/bin/bash
# Backup da base de dados WeatherDb (contentor Docker)

BACKUP_DIR="/opt/weather/backups"
DB_NAME="WeatherDb"
DB_USER="WeatherSolution"
DB_PASS="$(grep MYSQL_PASSWORD /opt/weather/.env | cut -d= -f2)"
DATE=$(date +%Y%m%d_%H%M%S)
FILE="$BACKUP_DIR/${DB_NAME}_${DATE}.sql.gz"
RETENTION_DAYS=30

mkdir -p "$BACKUP_DIR"

# Executa o mysqldump dentro do contentor do MySQL
docker compose -f /opt/weather/docker-compose.yml exec -T mysql \
  mysqldump --no-tablespaces -u "$DB_USER" -p"$DB_PASS" "$DB_NAME" | gzip > "$FILE"

# Verifica se o dump falhou (PIPESTATUS[0] é o mysqldump, não o gzip)
if [ "${PIPESTATUS[0]}" -ne 0 ]; then
  echo "$(date '+%Y-%m-%d %H:%M:%S') - ERRO: backup falhou" >&2
  rm -f "$FILE"
  exit 1
fi

echo "$(date '+%Y-%m-%d %H:%M:%S') - Backup OK: $FILE ($(du -h "$FILE" | cut -f1))"

# Remove backups com mais de RETENTION_DAYS dias
find "$BACKUP_DIR" -name "${DB_NAME}_*.sql.gz" -mtime +$RETENTION_DAYS -delete
