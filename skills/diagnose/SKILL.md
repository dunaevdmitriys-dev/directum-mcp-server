---
description: "Диагностика стенда Directum RX — проверка сервисов, логов, решений, ошибок"
---

# Diagnose — Диагностика стенда

Быстрая проверка здоровья стенда: сервисы, логи, опубликованные решения, типичные проблемы.

## Входные данные
- **launcher_path** — путь к DirectumLauncher (где `do.sh`)
- **symptom** (опционально) — описание проблемы от пользователя

## Все 26 сервисов платформы

| Сервис | Порт | Назначение | Проверка |
|--------|------|------------|----------|
| HAProxy | 80/443 | Балансировщик, SSL | `do.sh haproxy status` |
| SungeroWebServer | 8080 | ASP.NET backend | `do.sh webserver status` |
| SungeroWebClient | — | SPA frontend | `do.sh webclient status` |
| SungeroPublicApi | 5000 | OData REST API | `do.sh publicapi status` |
| PostgreSQL | 5432 | База данных | `do.sh postgres status` |
| PGBouncer | 6432 | Connection pooling | `do.sh pgbouncer status` |
| RabbitMQ | 5672 | Очередь сообщений | `do.sh rabbitmq status` |
| Elasticsearch | 9200 | Полнотекстовый поиск | `do.sh elasticsearch status` |
| Kibana | 5601 | Визуализация логов | `do.sh kibana status` |
| Minio | 9000/9001 | S3 storage | `do.sh minio status` |
| Centrifugo | 8000 | WebSocket push | `do.sh centrifugo status` |
| StorageService | — | Файловое хранилище | `do.sh platform status` |
| PreviewService | — | Превью документов | `do.sh platform status` |
| PreviewStorage | — | Кэш превью | `do.sh platform status` |
| JobScheduler | — | Фоновые задания | `do.sh platform status` |
| WorkflowBlockService | — | Блоки workflow | `do.sh platform status` |
| WorkflowProcessService | — | Процессы workflow | `do.sh platform status` |
| DelayedOperationsService | — | Отложенные операции | `do.sh platform status` |
| ReportService | — | Генерация отчётов | `do.sh platform status` |
| IntegrationService | — | 1C, Diadoc, SBIS | `do.sh platform status` |
| IndexingService | — | Индексация ES | `do.sh platform status` |
| GenericService | — | Универсальный | `do.sh platform status` |
| SungeroWidgets | — | Виджеты (Orleans) | `do.sh platform status` |
| ClientsConnectionService | — | Соединения клиентов | `do.sh platform status` |
| SungeroWorker | — | Фоновый процесс | `do.sh platform status` |
| DevelopmentStudio | 7190 | IDE (Electron+.NET) | `do.sh ds status` |

## Алгоритм

### 1. Сбор состояния (выполни ВСЁ параллельно)

```bash
# A. Здоровье сервисов
{launcher_path}/do platform check

# B. Опубликованные решения
{launcher_path}/do dt get-deployed-solutions

# C. Ошибки в текущем логе (последние 30 строк с ERROR)
grep -iE "ERROR|FAIL|Exception|WARN" {launcher_path}/log/current.log

# D. Последние 20 строк лога (контекст)
tail -20 {launcher_path}/log/current.log
```

### 2. Анализ

Проверь по таблице:

| Проверка | Признак проблемы | Действие |
|----------|-----------------|----------|
| platform check | Сервисы DOWN | `do platform up` |
| platform check | Частичный UP | Перезапуск: `do platform down && do platform up` |
| get-deployed-solutions | Пусто | Ничего не опубликовано — нужен deploy |
| get-deployed-solutions | Старая версия | Нужен повторный deploy с `--force` |
| Логи | `Connection refused` | Проверь БД и RabbitMQ |
| Логи | `Port in use` | Проверь порты: `netstat -an` |
| Логи | `Authentication failed` | Проверь credentials в config.yml |
| Логи | `OutOfMemory` | Нехватка RAM, проверь процессы |
| Логи | `Compilation error` | → агент Debugger |
| Логи | `Metadata error` | → агент Debugger |
| Логи | Нет ошибок | Стенд здоров |

#### Коды ошибок DeploymentTool (при деплое)

| Код | Значение | Рекомендация |
|-----|----------|-------------|
| 0 | Успех | — |
| 1 | Pre-deploy error | Проверь пакет: PackageInfo.xml, формат .dat |
| 2 | Deploy error | Проверь сеть, WebServer доступен, версии DTC = WebServer |
| 3 | Init error | Проверь ModuleInitializer, логи init |
| 4 | Settings error | Проверь settings/ в пакете |
| 5 | Import settings error | Проверь .datx файл |
| 7 | Export package error | Corrupt sources в git |

### 2b. Диагностика ресурсов и локализации

Если проблема связана с отсутствием подписей свойств, неверными названиями сущностей или пустыми лейблами:

| Симптом | Причина | Действие |
|---------|---------|----------|
| Пустые подписи свойств на карточке | System.resx содержит `Resource_<GUID>` вместо `Property_<Name>` | Заменить ключи в System.resx |
| Сущность отображается как «Справочник» | DisplayName не разрешён из .resx | Проверить `DisplayName` в Entity.resx/Entity.ru.resx |
| Обложка показывает ключи ресурсов | ResourcesKeys в Module.mtd указывают на несуществующие ключи .resx | Проверить Module.resx/Module.ru.resx |
| Satellite DLL отсутствует | DDS не собрал .ru.resources.dll | Пересобрать satellite assembly через dotnet SDK |
| Подписи есть в .resx, но не видны в UI | Satellite DLL не обновлена после изменения .resx | Пересобрать и задеплоить satellite DLL |

```bash
# Проверить satellite assemblies (внутри контейнеров RX)
docker compose -f deploy/docker-compose.rx.yml exec web ls /app/AppliedModules/ru/*.resources.dll 2>/dev/null

# Проверить ключи в исходных .resx файлах на наличие устаревшего формата Resource_<GUID>
grep -r "Resource_" {project_path}/source/**/*System*.resx && echo "[ОШИБКА] Найдены устаревшие ключи" || echo "[OK] Формат корректен"
grep -r "Property_" {project_path}/source/**/*System*.resx | head -10
```

**Быстрое исправление satellite DLL без полного ребилда:**
1. Исправить ключи в `*System.resx` / `*System.ru.resx` (GUID → PropertyName)
2. Скомпилировать .resx → .resources через `System.Resources.ResourceWriter`
3. Собрать satellite DLL через dotnet SDK
4. Остановить контейнеры RX: `docker compose stop` (из deploy/)
5. Заменить DLL в `AppliedModules/ru/` внутри контейнера
6. Перезапустить контейнеры: `docker compose up -d` (из deploy/)

### 3. Углублённая диагностика (если нужно)

```bash
# Проверка портов
lsof -i :80 -i :443 -i :5432 -i :5672 -i :7190 -i :10100

# Проверка config.yml (валидность)
python3 -c "import yaml; yaml.safe_load(open('{launcher_path}/etc/config.yml'))"

# Проверка PostgreSQL (через Docker)
docker compose -f deploy/docker-compose.infra.yml exec postgres psql -U directum -d directum -c "SELECT datname FROM pg_database;"

# Проверка RabbitMQ (через Docker)
docker compose -f deploy/docker-compose.infra.yml exec rabbitmq rabbitmqctl status

# Проверка дискового пространства
ls -la {launcher_path}

# Размер папки логов
du -sh {launcher_path}/log/

# Статус всех сервисов (одной командой)
{launcher_path}/do.sh all status

# Health check всех сервисов
{launcher_path}/do.sh all health

# Логи конкретного сервиса (последние 100 строк)
{launcher_path}/do.sh webserver logs --tail=100
{launcher_path}/do.sh jobscheduler logs --tail=100

# Список опубликованных решений (из БД, не из кэша)
{launcher_path}/do.sh dt get_deployed_solutions_from_db

# Перезагрузка конфига HAProxy (без рестарта)
{launcher_path}/do.sh haproxy reload_config
```

### 4. Отчёт

Выведи структурированный отчёт:

```
## Состояние стенда

### Сервисы: [OK / PARTIAL / DOWN]
- Всего: N сервисов
- Работает: N
- Не работает: [список]

### Решения: [N опубликовано]
- [Список решений с версиями]

### Ошибки: [N найдено / Нет]
- [Категория]: [Краткое описание]
- Рекомендация: [действие]

### Рекомендации
1. [Что сделать первым]
2. [Что сделать вторым]
```

## Быстрые команды восстановления

| Проблема | Команда |
|----------|---------|
| Все сервисы упали | `do platform up` |
| Нужен полный перезапуск | `do platform down && do platform up` |
| Пересоздать конфиг | `do platform config_up` |
| Переустановить платформу | `do platform install` |
| Переустановить DS | `do ds install` |
| Перезагрузка HAProxy | `do.sh haproxy reload_config` |
| Логи JobScheduler | `do.sh jobscheduler logs` |
| Логи WorkflowBlock | `do.sh workflowblock logs` |
| Пересоздание индекса ES | `do.sh elasticsearch configure` |

## Ссылки
- `.claude/agents/system-engineer.md` — управление инфраструктурой
- `.claude/agents/debugger.md` — глубокая диагностика ошибок
- `knowledge-base/guides/36_launcher_internals.md` — полная карта Launcher
- `knowledge-base/guides/38_platform_integration_map.md` — карта интеграций
