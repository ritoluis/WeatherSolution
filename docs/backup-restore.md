# Backup e Restauro da Base de Dados — WeatherDb

Este documento descreve como fazer backup e restauro da base de dados `WeatherDb` usando a ferramenta `mysqldump` do MySQL.

Há duas abordagens:

- **Opção A — Base de cópia (recomendada para testes)**: faz o teste numa base de dados separada, sem mexer na base real.
- **Opção B — Base real**: faz o backup, apaga e restaura a base real. Requer parar a API.

---

## Pré-requisitos

- MySQL instalado e a correr.
- Acesso ao `mysqldump` e ao `mysql` a partir da linha de comandos.
- Credenciais da base de dados (as mesmas que estão no `appsettings.json` da API):
  - Utilizador: `WeatherSolution`
  - Password: a que definiste
  - Base de dados: `WeatherDb`

Para confirmar que os comandos estão disponíveis, no CMD:

```cmd
mysqldump --version
mysql --version
```

---

## Opção A — Testar com Base de Cópia

Esta opção **não interrompe** a API nem a app MVC. É ideal para aprender o processo e validar que tudo funciona.

### A.1 — Fazer o backup da base original

```cmd
mysqldump -u WeatherSolution -p WeatherDb > WeatherDb_backup.sql
```

Introduz a password quando pedido. É criado o ficheiro `WeatherDb_backup.sql` na pasta atual.

### A.2 — Criar uma base de dados de cópia

```cmd
mysql -u WeatherSolution -p -e "CREATE DATABASE WeatherDb_test;"
```

### A.3 — Restaurar o backup na base de cópia

```cmd
mysql -u WeatherSolution -p WeatherDb_test < WeatherDb_backup.sql
```

### A.4 — Verificar que o restauro funcionou

```cmd
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb_test.Devices;"
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb_test.DeviceReadings;"
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb_test.Users;"
```

Os números devem coincidir com os da base original. Podes confirmar com:

```cmd
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb.Devices;"
```

### A.5 — Apagar a base de cópia

```cmd
mysql -u WeatherSolution -p -e "DROP DATABASE WeatherDb_test;"
```

### A.6 — (Opcional) Apagar o ficheiro de backup

Se já não precisares dele para este teste, podes apagá-lo manualmente.

---

## Opção B — Testar com a Base Real

**Atenção**: esta opção apaga a base de dados `WeatherDb` real. Só deve ser feita depois de teres validado o processo com a Opção A, e com a certeza de que o ficheiro de backup está íntegro.

### B.0 — Parar a API, a app MVC e os dispositivos

Antes de começar:

1. Fecha a API (Visual Studio, ou o processo).
2. Fecha a app MVC.
3. Desliga o ESP32 (ou desliga o Wi-Fi dele), para não tentar comunicar durante o processo.

O `mysqldump` (backup) não precisa disto, mas o `DROP DATABASE` (apagar) e o restauro **sim** — a API não pode aceder a tabelas a meio da criação.

### B.1 — Fazer o backup da base real

```cmd
mysqldump -u WeatherSolution -p WeatherDb > WeatherDb_backup.sql
```

Confirma que o ficheiro existe e não está vazio:

```cmd
dir WeatherDb_backup.sql
```

Abre-o num editor de texto e confirma que contém `CREATE TABLE` e `INSERT INTO`.

### B.2 — Apagar a base de dados

```cmd
mysql -u WeatherSolution -p -e "DROP DATABASE WeatherDb;"
```

### B.3 — Criar a base de dados vazia novamente

```cmd
mysql -u WeatherSolution -p -e "CREATE DATABASE WeatherDb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
```

**Nota**: o `CHARACTER SET utf8mb4` é o mesmo usado pelo EF Core. Sem isto, podem surgir problemas de codificação.

### B.4 — Restaurar o backup

```cmd
mysql -u WeatherSolution -p WeatherDb < WeatherDb_backup.sql
```

### B.5 — Verificar o restauro

```cmd
mysql -u WeatherSolution -p -e "USE WeatherDb; SHOW TABLES;"
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb.Devices;"
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb.DeviceReadings;"
mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb.Users;"
```

Deves ver as tabelas `Users`, `Devices`, `DeviceReadings`, `TemperatureReadings`, `UserQueries` e `__EFMigrationsHistory`. E as contagens devem coincidir com o que tinhas antes.

### B.6 — Voltar a ligar a API, a app MVC e os dispositivos

1. Liga a API. Confirma que arranca sem erros.
2. Liga a app MVC. Faz login e confirma que os dados estão acessíveis.
3. Liga o ESP32. Confirma que se liga ao MQTT e que a API responde.

---

## Automatizar o Backup (Windows)

Para não dependeres de fazer o backup manualmente, podes agendar uma tarefa no **Agendador de Tarefas do Windows**.

### Criar um script `backup.bat`

Coloca este ficheiro numa pasta segura (por exemplo, `C:\Backups\`):

```bat
@echo off
setlocal

set DB_USER=WeatherSolution
set DB_PASS=A_TUA_PASSWORD
set DB_NAME=WeatherDb
set BACKUP_DIR=C:\Backups\WeatherDb
set TIMESTAMP=%date:~-4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%
set TIMESTAMP=%TIMESTAMP: =0%

if not exist "%BACKUP_DIR%" mkdir "%BACKUP_DIR%"

mysqldump -u %DB_USER% -p%DB_PASS% %DB_NAME% > "%BACKUP_DIR%\WeatherDb_%TIMESTAMP%.sql"

echo Backup concluído: %BACKUP_DIR%\WeatherDb_%TIMESTAMP%.sql
endlocal
```

**Nota**: a password fica em claro no ficheiro `.bat`. Protege o acesso ao ficheiro com permissões restritas, ou usa um ficheiro `.cnf` de configuração do MySQL com as credenciais (mais seguro).

### Agendar no Agendador de Tarefas

1. Abre o **Agendador de Tarefas** (`taskschd.msc`).
2. **Criar Tarefa Básica**.
3. Nome: `Backup WeatherDb`.
4. Frequência: **Diariamente**, à hora que quiseres (por exemplo, 03:00).
5. Ação: **Iniciar um programa**.
6. Programa/script: `C:\Backups\backup.bat`.
7. Concluir.

---

## Restaurar um Backup

Para restaurar um backup (em qualquer altura), o processo é o mesmo do **Passo B.4**:

```cmd
mysql -u WeatherSolution -p WeatherDb < WeatherDb_backup.sql
```

Se a base de dados atual ainda existir e quiseres substituí-la completamente, primeiro apaga-a:

```cmd
mysql -u WeatherSolution -p -e "DROP DATABASE WeatherDb;"
mysql -u WeatherSolution -p -e "CREATE DATABASE WeatherDb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"
mysql -u WeatherSolution -p WeatherDb < WeatherDb_backup.sql
```

---

## Notas Importantes

- **O backup inclui tudo**: tabelas, dados, índices, chaves estrangeiras. É uma cópia fiel da base de dados.
- **O backup não inclui** os ficheiros do projeto nem a configuração da API. Se mudares de servidor, precisas também de:
  - Copiar a pasta do projeto (ou clonar do GitHub).
  - Copiar o `appsettings.json` com as credenciais.
  - Reconfigurar o Mosquitto (broker MQTT).
  - Reconfigurar o certificado HTTPS (Let's Encrypt em produção).
- **A password do utilizador da BD** não é exportada em claro — os `CREATE USER` só aparecem se usares `--all-databases` ou `--users`. No cenário acima, o utilizador `WeatherSolution` tem de existir na base de destino antes do restauro.
- **O ficheiro de backup contém hashes** (BCrypt) das passwords dos utilizadores e das API Keys dos dispositivos. Trata-o como informação sensível — guarda-o em local seguro.

---

## Resumo dos Comandos

| Ação | Comando |
|---|---|
| Backup | `mysqldump -u WeatherSolution -p WeatherDb > WeatherDb_backup.sql` |
| Criar base | `mysql -u WeatherSolution -p -e "CREATE DATABASE WeatherDb;"` |
| Apagar base | `mysql -u WeatherSolution -p -e "DROP DATABASE WeatherDb;"` |
| Restaurar | `mysql -u WeatherSolution -p WeatherDb < WeatherDb_backup.sql` |
| Ver tabelas | `mysql -u WeatherSolution -p -e "USE WeatherDb; SHOW TABLES;"` |
| Contar registos | `mysql -u WeatherSolution -p -e "SELECT COUNT(*) FROM WeatherDb.Devices;"` |


---

## Automatizar o Backup (Linux)

No servidor Linux (Ubuntu/Debian, por exemplo), o agendamento é feito com `cron`. O processo é semelhante ao do Windows, mas com scripts de shell.

### Pré-requisitos

- `mysqldump` e `mysql` instalados (vêm com o pacote `mysql-client` ou `mariadb-client`):
  ```bash
  sudo apt update
  sudo apt install mysql-client
  ```
- Um diretório para guardar os backups, com espaço suficiente:
  ```bash
  sudo mkdir -p /var/backups/weatherdb
  sudo chown $USER:$USER /var/backups/weatherdb
  ```

### Criar o script `backup.sh`

Coloca o ficheiro em `/home/<utilizador>/scripts/backup-weatherdb.sh` (ou noutro caminho à tua escolha):

```bash
#!/bin/bash
# ============================================================
#  Backup da base de dados WeatherDb
# ============================================================

set -euo pipefail

# Configuração
DB_USER="WeatherSolution"
DB_PASS="A_TUA_PASSWORD"
DB_NAME="WeatherDb"
BACKUP_DIR="/var/backups/weatherdb"
RETENTION_DAYS=30

# Timestamp
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BACKUP_FILE="${BACKUP_DIR}/${DB_NAME}_${TIMESTAMP}.sql"

# Garante que o diretório existe
mkdir -p "${BACKUP_DIR}"

# Faz o backup
echo "[$(date)] A iniciar backup de ${DB_NAME}..."
mysqldump -u "${DB_USER}" -p"${DB_PASS}" "${DB_NAME}" > "${BACKUP_FILE}"

# Comprime
gzip "${BACKUP_FILE}"
echo "[$(date)] Backup concluído: ${BACKUP_FILE}.gz"

# Remove backups com mais de RETENTION_DAYS dias
find "${BACKUP_DIR}" -name "${DB_NAME}_*.sql.gz" -type f -mtime +${RETENTION_DAYS} -delete
echo "[$(date)] Backups com mais de ${RETENTION_DAYS} dias removidos."
```

**Nota sobre a password**: fica em claro no script. Para maior segurança, cria um ficheiro `~/.my.cnf` com as credenciais e remove a password do script:

**`~/.my.cnf`** (com permissões `600`):
```ini
[client]
user=WeatherSolution
password=A_TUA_PASSWORD
```

E altera o script para usar só:
```bash
mysqldump "${DB_NAME}" > "${BACKUP_FILE}"
```

Protege o ficheiro:
```bash
chmod 600 ~/.my.cnf
```

### Tornar o script executável

```bash
chmod +x ~/scripts/backup-weatherdb.sh
```

### Testar o script manualmente

```bash
~/scripts/backup-weatherdb.sh
```

Confirma que o ficheiro `.sql.gz` foi criado em `/var/backups/weatherdb/`:

```bash
ls -lh /var/backups/weatherdb/
```

### Agendar com `cron`

Abre o editor de `cron` do utilizador atual:

```bash
crontab -e
```

Adiciona esta linha no fim (backup diário às 03:00):

```
0 3 * * * /home/<utilizador>/scripts/backup-weatherdb.sh >> /var/log/weatherdb-backup.log 2>&1
```

**Explicação:**
- `0 3 * * *` — todos os dias às 03:00.
- O resto é o comando a executar.
- `>> /var/log/weatherdb-backup.log 2>&1` — redireciona stdout e stderr para um ficheiro de log, útil para diagnosticar problemas.

Confirma que o `cron` está a correr:

```bash
sudo systemctl status cron
```

E que a tarefa foi registada:

```bash
crontab -l
```

### Verificar o log

Depois da primeira execução (no dia seguinte, ou se alterares a hora para testar), verifica:

```bash
cat /var/log/weatherdb-backup.log
```

Deves ver linhas com o timestamp e "Backup concluído".

---

## Restaurar um Backup (Linux)

O processo é idêntico ao do Windows, mas em bash:

```bash
# Descomprimir o backup (se estiver em .gz)
gunzip -c /var/backups/weatherdb/WeatherDb_20261014_030000.sql.gz > /tmp/restore.sql

# Apagar a base atual (opcional)
mysql -u WeatherSolution -p -e "DROP DATABASE WeatherDb;"

# Criar a base de novo
mysql -u WeatherSolution -p -e "CREATE DATABASE WeatherDb CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# Restaurar
mysql -u WeatherSolution -p WeatherDb < /tmp/restore.sql

# Limpar o ficheiro temporário
rm /tmp/restore.sql
```

Ou, numa única linha (sem descomprimir para ficheiro):

```bash
gunzip -c /var/backups/weatherdb/WeatherDb_20261014_030000.sql.gz | mysql -u WeatherSolution -p WeatherDb
```

---

## Backup Remoto (Linux) — opcional

Para maior segurança, podes enviar os backups para outro servidor ou para a cloud (S3, Backblaze, etc.). Um exemplo com `scp`:

```bash
# No fim do backup.sh, depois de comprimir:
scp "${BACKUP_FILE}.gz" user@backup-server:/backups/weatherdb/
```

Ou com `rclone` para sincronizar com Google Drive, OneDrive, S3, etc.:

```bash
rclone copy "${BACKUP_FILE}.gz" remote:weatherdb-backups/
```

Esta camada extra protege-te contra o cenário em que o próprio servidor falha por completo (disco, incêndio, ransomware). Sem ela, o backup fica no mesmo sítio que a base de dados — se o servidor morrer, perdes ambos.

---

## Comparação Windows vs Linux

| Tarefa | Windows | Linux |
|---|---|---|
| Agendador | Task Scheduler (`taskschd.msc`) | `cron` (`crontab -e`) |
| Script | `.bat` | `.sh` |
| Local do backup | `C:\Backups\` | `/var/backups/` |
| Comprimir | `gzip`/`7z` manual | `gzip` integrado no script |
| Retenção | Manual ou via script | `find ... -mtime +N -delete` |
| Log | `>> log.txt` | `>> log.txt 2>&1` |

---

## Notas Finais sobre Retenção

Escolhe uma política de retenção consoante a criticidade dos dados:

| Política | Quando usar |
|---|---|
| **7 dias** | Ambiente de desenvolvimento ou testes |
| **30 dias** | Aplicação em produção pequena, dados recuperáveis |
| **90 dias + mensal + anual** | Dados críticos, requisitos de compliance |

O script acima usa **30 dias** por defeito. Se quiseres guardar backups mensais e anuais, tens de ter uma lógica adicional (por exemplo, marcar o backup do primeiro dia de cada mês como "mensal" e não o apagar nunca).

**Regra de ouro**: um backup que nunca foi testado a restaurar **não é um backup** — é uma esperança. Testa o restauro periodicamente (por exemplo, uma vez por trimestre) numa máquina separada.