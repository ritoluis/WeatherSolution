# Deploy

Este documento descreve o deploy do WeatherSolution em produção: o servidor, o Docker, o Nginx, o HTTPS, o DDNS, e os processos automáticos.

## 1. Visão geral

O sistema corre num servidor caseiro com Debian 13 (Trixie), sem interface gráfica. Todos os serviços correm em contentores Docker, orquestrados por Docker Compose.

O acesso externo é feito por HTTPS, com certificado Let's Encrypt, e por um domínio DuckDNS que aponta para o IP público do router.

## 2. Servidor

| Item | Valor |
|---|---|
| Distro | Debian 13 (Trixie) |
| Arquitetura | amd64 |
| Processador | Intel i3 |
| RAM | 4 GB |
| Disco | 144 GB |
| Acesso | SSH, com utilizador não-root e sudo |
| Localização do projeto | /opt/weather |

O servidor não tem interface gráfica. Toda a gestão é feita por SSH.

## 3. Docker

O Docker Engine é instalado pelo repositório oficial da Docker, não pelo apt install docker.io.

| Contentor | Imagem | Porta exposta |
|---|---|---|
| weather-mysql | mysql:8.0 | 127.0.0.1:3306 (só local) |
| weather-mosquitto | eclipse-mosquitto:2.0 | 0.0.0.0:1883 |
| weather-api | weather-api (build local) | 127.0.0.1:5000 |
| weather-webapp | weather-webapp (build local) | 127.0.0.1:5001 |
| weather-nginx | nginx:stable-alpine | 0.0.0.0:80 e 0.0.0.0:443 |

O MySQL, a API e a WebApp só escutam em 127.0.0.1. O acesso externo passa sempre pelo Nginx. O Mosquitto escuta em todas as interfaces, porque os dispositivos ligam-se diretamente.

## 4. Docker Compose

O ficheiro docker-compose.yml define os cinco serviços, a rede interna (weather-net), e os volumes persistentes.

### Variáveis de ambiente

As variáveis sensíveis estão no ficheiro .env, que não vai para o Git:

| Variável | Usada por |
|---|---|
| MYSQL_ROOT_PASSWORD | MySQL |
| MYSQL_PASSWORD | MySQL e API |
| JWT_KEY | API |
| MQTT_API_PASSWORD | API |

O .env é referenciado pelo docker-compose.yml com ${VARIAVEL}.

### Volumes

| Volume | Conteúdo |
|---|---|
| ./mysql/data | Dados do MySQL |
| ./mosquitto/config | Configuração do Mosquitto |
| ./mosquitto/data | Dados do Mosquitto |
| ./mosquitto/log | Logs do Mosquitto |
| ./nginx/conf.d | Configuração do Nginx |
| ./nginx/certbot-webroot | Desafios do Certbot |
| ./certbot/conf | Certificados Let's Encrypt |

## 5. Nginx

O Nginx é o reverse proxy. Recebe os pedidos nas portas 80 e 443, e encaminha-os para os serviços internos:

| Caminho | Destino |
|---|---|
| / | webapp:8080 |
| /api/ | api:8080 |
| /swagger/ | api:8080 (só em desenvolvimento) |

A configuração está em nginx/conf.d/default.conf.

### Redirecionamento HTTP → HTTPS

O servidor que escuta na porta 80 redireciona tudo para HTTPS, exceto o caminho /.well-known/acme-challenge/, que é usado pelo Certbot para validar o domínio.

### Configuração HTTPS

O servidor que escuta na porta 443 usa o certificado em /etc/letsencrypt/live/ritoluis.duckdns.org/. A configuração inclui os headers X-Forwarded-* para que a API saiba o IP real do cliente.

## 6. HTTPS com Let's Encrypt

O certificado é emitido pelo Certbot, em modo webroot.

### Emissão inicial
sudo docker compose run --rm --entrypoint certbot certbot certonly --webroot
--webroot-path=/var/www/certbot
-d ritoluis.duckdns.org
--email luis.rito.1978@gmail.com
--agree-tos --no-eff-email

text

Os ficheiros ficam em /opt/weather/certbot/conf/live/ritoluis.duckdns.org/.

### Renovação automática

A renovação é feita por um cron no host, que corre o script /opt/weather/renew-cert.sh duas vezes por dia (00:00 e 12:00). O script:

1. Corre certbot renew (só renova se faltarem menos de 30 dias).
2. Recarrega o Nginx.

O log fica em /var/log/certbot-renew.log.

## 7. DuckDNS

O domínio ritoluis.duckdns.org aponta para o IP público do router. Como o IP é dinâmico, o DuckDNS é atualizado automaticamente.

### Script de atualização

O script /opt/weather/duckdns-update.sh:

1. Obtém o IP público atual.
2. Chama a API do DuckDNS.
3. Verifica a resposta (OK ou KO).
4. Regista o resultado no log.

O script corre a cada 5 minutos, por cron.

### Log

O log fica em /var/log/duckdns-update.log.

## 8. Port forwarding

No router (MEO FiberGateway), foram criadas regras de port forwarding:

| Porta externa | Porta interna | IP interno |
|---|---|---|
| 80 | 80 | 192.168.1.86 |
| 443 | 443 | 192.168.1.86 |
| 1883 | 1883 | 192.168.1.86 |

O IP interno do servidor (192.168.1.86) está reservado por uma lease estática no router, para não mudar após reinícios.

## 9. Backup automático

O backup da base de dados é feito por um cron no host, que corre o script /opt/weather/backup-db.sh diariamente às 03:00.

O script:

1. Corre mysqldump dentro do contentor do MySQL, com --no-tablespaces.
2. Comprime o resultado com gzip.
3. Guarda em /opt/weather/backups/.
4. Apaga backups com mais de 30 dias.

O log fica em /var/log/weatherdb-backup.log.

## 10. Cron

O crontab do utilizador luis tem duas linhas:
*/5 * * * * /opt/weather/duckdns-update.sh >> /var/log/duckdns-update.log 2>&1
0 3 * * * /opt/weather/backup-db.sh >> /var/log/weatherdb-backup.log 2>&1

text

A renovação do certificado é feita por um cron separado, no crontab do root (ou no mesmo, com sudo). O script renew-cert.sh corre duas vezes por dia.

## 11. Atualização do sistema

Para atualizar o código em produção:

1. No servidor: cd /opt/weather e git pull origin dev.
2. Se houver alterações no código da API ou da WebApp: sudo docker compose up -d --build api webapp.
3. Se houver alterações no docker-compose.yml: sudo docker compose up -d.

As alterações ao código são feitas no PC, commitadas e enviadas para a branch dev, e depois trazidas para o servidor com git pull.

## 12. Acesso

| Serviço | URL |
|---|---|
| App web | https://ritoluis.duckdns.org |
| API | https://ritoluis.duckdns.org/api/... |
| Swagger | (não exposto em produção) |
| MQTT | ritoluis.duckdns.org:1883 |

## 13. Segurança

- O servidor não tem interface gráfica.
- O acesso SSH é feito com utilizador não-root.
- As passwords e chaves estão no .env, que não vai para o Git.
- O ficheiro passwd.txt do Mosquitto não vai para o Git.
- O MySQL só escuta em 127.0.0.1.
- O Nginx é o único ponto de entrada HTTP/HTTPS.
- O certificado é renovado automaticamente.
- O rate limiting protege a API contra abusos.

## 14. Limitações atuais

- O MQTT não usa TLS. O tráfego dos dispositivos circula em claro na internet.
- O servidor é caseiro, sem redundância nem UPS. Uma falha de energia prolongada pode causar downtime.
- O IP público é dinâmico. Se mudar, o DuckDNS é atualizado em menos de 5 minutos, mas há uma janela de indisponibilidade.
- O backup é local (no mesmo servidor). Se o disco falhar, perdem-se os dados e os backups. Para produção, deveria haver backup remoto (S3, outro servidor, etc.).
- A renovação do certificado depende do cron. Se o cron falhar, o certificado expira (90 dias).

## 15. Ficheiros principais

| Ficheiro | Função |
|---|---|
| /opt/weather/docker-compose.yml | Definição dos serviços |
| /opt/weather/.env | Variáveis sensíveis (não versionado) |
| /opt/weather/nginx/conf.d/default.conf | Configuração do Nginx |
| /opt/weather/mosquitto/config/mosquitto.conf | Configuração do Mosquitto |
| /opt/weather/renew-cert.sh | Renovação do certificado |
| /opt/weather/duckdns-update.sh | Atualização do DuckDNS |
| /opt/weather/backup-db.sh | Backup da base de dados |