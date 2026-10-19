#!/bin/bash
cd /opt/weather
docker compose run --rm --entrypoint certbot certbot renew --quiet
docker compose exec nginx nginx -s reload
