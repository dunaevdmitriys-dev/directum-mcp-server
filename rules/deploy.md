---
paths:
  - "deploy/**"
---

# Docker Infrastructure

## Два compose-файла
- `docker-compose.infra.yml` — PostgreSQL 15.3 (:5432), RabbitMQ 4.0 (:5672/15672), Elasticsearch 7.17.23 (:9200)
- `docker-compose.rx.yml` — RX stack: HAProxy (:8080), WebServer (:44310), WebClient (:44320), StorageService (:44330), IntegrationService (:44340), Worker, JobScheduler, Grains (Orleans)

## Credentials
- PostgreSQL: directum / directum / directum
- RabbitMQ: directum / directum
- Elasticsearch: без авторизации
- RX Web Client: http://localhost:8080/Client (Administrator/11111)
- OData: http://localhost:8080/Integration/odata/

## Volumes
`deploy/data/` — postgres, rabbitmq, elasticsearch, home, logs

## Конфигурация RX
Сервисы читают `WEB_HOST_HTTP_PORT=44310` из config.xml в `дистрибутив/launcher/etc/`
