---
description: "Управление сервисами Directum RX через DirectumLauncher: up/down/status/health/logs"
---

# Launcher Service — Управление сервисами платформы

Управление 26 сервисами Directum RX через DirectumLauncher CLI.

## Входные данные

- **launcher_path** — путь к DirectumLauncher (определяется автоматически: `дистрибутив/launcher` или `дистрибутив/DirectumLauncher`)
- **action** — действие: `status`, `up`, `down`, `restart`, `logs`, `health`
- **service** — сервис (опц.): `all`, `webserver`, `postgres`, `rabbitmq`, `elasticsearch`, `haproxy`, `kibana`, `minio`, `centrifugo`, `pgbouncer`, `platform`, `ds`

## Алгоритм

### 1. Определи launcher_path
```bash
# Определи путь к Launcher через переменную окружения LAUNCHER_PATH
# или найди дистрибутив в рабочем пространстве:
#   Glob("дистрибутив/DirectumLauncher/do.sh") или Glob("дистрибутив/launcher/do.sh")
LAUNCHER="${LAUNCHER_PATH:-$(pwd)/дистрибутив/launcher}"
# Альтернатива: LAUNCHER="$(pwd)/дистрибутив/DirectumLauncher"
```

### 2. Выполни действие

#### Статус всех сервисов
```bash
$LAUNCHER/do.sh all status
```

#### Запуск всех / конкретного
```bash
$LAUNCHER/do.sh all up                    # Все сервисы
$LAUNCHER/do.sh all up --exclude="kibana"  # Все кроме Kibana
$LAUNCHER/do.sh postgres up               # Только PostgreSQL
$LAUNCHER/do.sh rabbitmq up               # Только RabbitMQ
$LAUNCHER/do.sh platform up               # Все платформенные сервисы
```

#### Остановка
```bash
$LAUNCHER/do.sh all down
$LAUNCHER/do.sh all down --exclude="postgres,rabbitmq"  # Оставить инфру
$LAUNCHER/do.sh platform down             # Только платформенные
```

#### Перезапуск
```bash
$LAUNCHER/do.sh all down && $LAUNCHER/do.sh all up
$LAUNCHER/do.sh webserver down && $LAUNCHER/do.sh webserver up
```

#### Health check
```bash
$LAUNCHER/do.sh all health               # Все сервисы
$LAUNCHER/do.sh postgres health          # Конкретный
```

#### Логи
```bash
$LAUNCHER/do.sh webserver logs            # Последние логи WebServer
$LAUNCHER/do.sh jobscheduler logs         # JobScheduler
$LAUNCHER/do.sh rabbitmq logs             # RabbitMQ

# Launcher-логи (Python)
cat $LAUNCHER/log/current.log             # Текущий запуск
tail -100 $LAUNCHER/log/all.log           # Все запуски
```

### 3. Управление конфигурацией
```bash
# Валидация config.yml
$LAUNCHER/do.sh config validate

# Шифрование конфига
$LAUNCHER/do.sh config encrypt --value="password" --input=etc/config.yml --output=etc/config.yml.enc

# Генерация HAProxy config
$LAUNCHER/do.sh haproxy reload_config
```

### 4. Управление DevelopmentStudio
```bash
$LAUNCHER/do.sh ds run                    # Запустить DDS Desktop
$LAUNCHER/do.sh ds install                # Установить DDS
$LAUNCHER/do.sh ds uninstall              # Удалить DDS
```

### 5. Управление пакетами/решениями
```bash
# Список опубликованных решений
$LAUNCHER/do.sh dt get_deployed_solutions

# Деплой .dat
$LAUNCHER/do.sh dt deploy --package="path.dat" --force

# Удаление решений
$LAUNCHER/do.sh dt remove_solutions --solution_names="DirRX.CRM"
```

## Сервисы и порты

| Namespace | Сервис | Порт | Действия |
|-----------|--------|------|----------|
| `all` | Все сервисы | — | up, down, status, health, logs |
| `postgres` | PostgreSQL | 5432 | up, down, status, health, logs |
| `rabbitmq` | RabbitMQ | 5672/15672 | up, down, status, health, logs, info |
| `elasticsearch` | Elasticsearch | 9200 | up, down, status, health, logs |
| `kibana` | Kibana | 5601 | up, down, status, health, logs |
| `haproxy` | HAProxy | 80/443 | up, down, status, health, logs, reload_config |
| `minio` | MinIO | 9000/9001 | up, down, status, health, logs |
| `pgbouncer` | PGBouncer | 6432 | up, down, status, health, logs |
| `centrifugo` | Centrifugo | 8000 | up, down, status, health |
| `platform` | 16+ RX сервисов | — | up, down, status, check |
| `dt` | DeploymentTool | — | deploy, export-package, merge_packages, get_deployed_solutions |
| `ds` | DevelopmentStudio | 7190 | run, install, uninstall |

## Ссылки
- `knowledge-base/guides/36_launcher_internals.md` — полная архитектура Launcher
- `knowledge-base/guides/38_platform_integration_map.md` — карта интеграций
- `.claude/skills/deploy/SKILL.md` — сборка и деплой .dat
- `.claude/skills/diagnose/SKILL.md` — диагностика стенда
