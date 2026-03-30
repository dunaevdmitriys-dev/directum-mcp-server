# Карта интеграций платформы Directum RX 26.1

> Как DeploymentToolCore, DirectumLauncher и DevelopmentStudio работают вместе.
> Используй этот гайд для понимания полного цикла: код → сборка → деплой → работающая система.

---

## 1. Общая архитектура

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        РАЗРАБОТКА                                       │
│                                                                         │
│  DevelopmentStudio (CrossPlatform DS)                                   │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐              │
│  │ Electron UI  │←──→│ .NET Backend │←──→│ Git Repo     │              │
│  │ (app.asar)   │    │ (port 7190)  │    │ (work/base)  │              │
│  └──────────────┘    └──────────────┘    └──────┬───────┘              │
│                                                  │                      │
│  VS Code Extension ─── навигация к исходникам    │                      │
│                                                  │ .mtd / .cs / .resx   │
├──────────────────────────────────────────────────┼──────────────────────┤
│                        СБОРКА                    │                      │
│                                                  ▼                      │
│  DirectumLauncher (do.sh)                                               │
│  ┌──────────────────────────────────────────────────────┐              │
│  │ do.sh dt export-package                               │              │
│  │   → Читает git repo                                   │              │
│  │   → Компилирует C# (Roslyn)                           │              │
│  │   → Создаёт .dat (ZIP: PackageInfo.xml + source/)     │              │
│  └──────────────────────────────┬───────────────────────┘              │
│                                 │                                       │
│  ┌──────────────────────────────┼───────────────────────┐              │
│  │ do.sh dt merge_packages     │                         │              │
│  │   → Объединяет несколько .dat в один                  │              │
│  └──────────────────────────────┼───────────────────────┘              │
│                                 │ .dat пакет                           │
├─────────────────────────────────┼──────────────────────────────────────┤
│                        ДЕПЛОЙ   │                                       │
│                                 ▼                                       │
│  DeploymentToolCore (Docker-контейнер)                                  │
│  ┌──────────────────────────────────────────────────────┐              │
│  │ do.sh dt deploy --package="*.dat"                     │              │
│  │   1. Публикация сборок → WebServer                    │              │
│  │   2. Инициализация → DB schema (--init)               │              │
│  │   3. Настройки → бизнес-процессы (--settings)         │              │
│  └──────────────────────────────┬───────────────────────┘              │
│                                 │                                       │
├─────────────────────────────────┼──────────────────────────────────────┤
│                        RUNTIME  │                                       │
│                                 ▼                                       │
│  26 Platform Services (Docker containers)                               │
│  ┌─────────────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐     │
│  │ WebServer   │ │ WebClient   │ │ PublicApi   │ │ JobScheduler│     │
│  │ (ASP.NET)   │ │ (SPA)       │ │ (OData)     │ │ (Cron)      │     │
│  └──────┬──────┘ └─────────────┘ └─────────────┘ └─────────────┘     │
│         │                                                               │
│  ┌──────┴──────┐ ┌─────────────┐ ┌─────────────┐ ┌─────────────┐     │
│  │ HAProxy     │ │ PostgreSQL  │ │ RabbitMQ    │ │ Elasticsearch│    │
│  │ (LB + SSL)  │ │ (DB)        │ │ (Queue)     │ │ (Search)     │    │
│  └─────────────┘ └─────────────┘ └─────────────┘ └─────────────┘     │
└─────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Data Flow: от кода до работающей системы

### 2.1 Исходный код → Git-репозиторий

| Артефакт | Формат | Кто создаёт | Где хранится |
|----------|--------|-------------|-------------|
| Метаданные сущностей | `.mtd` (JSON) | DDS / Claude Code | `source/<Module>/<Entity>/` |
| Серверный код | `.cs` (C#) | DDS / VS Code / Claude | `source/<Module>/<Entity>/` |
| Локализация | `.resx` (XML) | DDS / Claude Code | `source/<Module>/<Entity>/` |
| SQL-запросы | `.sql` | Разработчик | `source/<Module>/<Entity>/` |
| Workflow-схемы | RouteScheme в `.mtd` | DDS / Claude Code | `source/<Module>/<Entity>/` |

### 2.2 Git → .dat пакет

```bash
# Экспорт из git-репозитория
do.sh dt export-package \
  --export_package="/output/MyModule.dat" \
  --configuration="/output/config.xml" \
  --root="/git_repository" \
  --repositories="work"
```

**Конфигурационный XML:**
```xml
<?xml version="1.0"?>
<DevelopmentPackageInfo>
  <IsDebugPackage>false</IsDebugPackage>
  <PackageModules>
    <PackageModuleItem>
      <Id>{guid}</Id>
      <Name>DirRX.MyModule</Name>
      <Version>1.0.0.0</Version>
      <IncludeAssemblies>true</IncludeAssemblies>
      <IncludeSources>false</IncludeSources>
      <IsSolution>true</IsSolution>
    </PackageModuleItem>
  </PackageModules>
</DevelopmentPackageInfo>
```

**Формат .dat пакета (ZIP):**
```
Package.dat
├── PackageInfo.xml          ← Метаданные: модули, версии, зависимости
├── source/
│   └── DirRX.MyModule/
│       ├── Module/
│       │   ├── Module.mtd
│       │   ├── ModuleHandlers.cs
│       │   └── ModuleInitializer.cs
│       ├── MyEntity/
│       │   ├── MyEntity.mtd
│       │   ├── MyEntityHandlers.cs
│       │   └── MyEntity.resx
│       └── ...
└── settings/                ← Бизнес-настройки (опционально)
```

### 2.3 .dat → Работающая система

```bash
# Публикация пакета
do.sh dt deploy --package="/output/MyModule.dat"

# Что происходит внутри:
# 1. DTC распаковывает .dat
# 2. Компилирует C# → .dll сборки
# 3. Копирует сборки в WebServer
# 4. Генерирует SQL → создаёт/мигрирует таблицы в PostgreSQL
# 5. Регистрирует модули в системном каталоге
# 6. (--init) Запускает ModuleInitializer каждого модуля
# 7. (--settings) Применяет настройки бизнес-процессов
```

---

## 3. Config Flow: config.yml → все сервисы

```
config.yml (единый файл конфигурации)
    │
    ├── Jinja2 templating
    │   ├── {{ host_ip }}           → автоопределение IP хоста
    │   ├── {{ home_path }}         → корневая директория (/opt/directum)
    │   ├── {{ getenv("VAR") }}     → переменные окружения
    │   └── {% if %}...{% endif %}  → условные секции
    │
    ├── variables (верхнеуровневые)
    │   ├── instance_name           → имя инстанса (→ Docker container prefix)
    │   ├── host_fqdn               → FQDN хоста (→ SSL, HAProxy)
    │   ├── http_port / https_port  → порты (→ HAProxy, WebServer)
    │   └── protocol                → http/https (→ DeploymentTool, WebServer)
    │
    └── services_config (30+ секций)
        ├── SungeroWebServer     → SERVER_ROOT, ports, protocol
        ├── SungeroWebClient     → frontend bundle
        ├── SungeroPublicApi     → OData endpoint
        ├── DeploymentTool       → WEB_RELATIVE_PATH, auth, ports
        ├── DevelopmentStudioDesktop → COMPANY_CODE, GIT_ROOT, REPOSITORIES
        ├── PostgreSQL           → CONNECTION_STRING
        ├── RabbitMQ             → QUEUE_CONNECTION_STRING, ports
        ├── Elasticsearch        → ELASTICSEARCH_URL, index
        ├── HAProxy              → backend servers, health checks
        └── ...26 остальных сервисов
```

### Ключевые зависимости конфигурации

| Переменная config.yml | Куда передаётся | Как используется |
|----------------------|-----------------|-----------------|
| `CONNECTION_STRING` | DTC, WebServer, JobScheduler, WorkflowBlock/Process, IndexingService | Подключение к PostgreSQL |
| `QUEUE_CONNECTION_STRING` | WebServer, WorkflowBlock, JobScheduler, DelayedOps | Подключение к RabbitMQ |
| `ELASTICSEARCH_URL` | IndexingService, InitialIndexing | Полнотекстовый поиск |
| `AUTHENTICATION_USERNAME/PASSWORD` | DeploymentTool | Авторизация при деплое |
| `SERVER_ROOT` | DeploymentTool, HAProxy | Hostname веб-сервера |
| `WEB_PROTOCOL` | DeploymentTool | http/https для API-вызовов |
| `COMPANY_CODE` | DevelopmentStudio | Префикс пространств имён |
| `GIT_ROOT_DIRECTORY` | DevelopmentStudio | Корень git-репозиториев |

---

## 4. Компонентные зависимости

### 4.1 Порядок запуска сервисов

```
Уровень 0 (инфраструктура, нет зависимостей):
  PostgreSQL → RabbitMQ → Elasticsearch → Minio

Уровень 1 (зависят от инфры):
  PGBouncer (← PostgreSQL)
  HAProxy (← ничего, но маршрутизирует к WebServer)

Уровень 2 (платформенные сервисы):
  StorageService      ← PostgreSQL, (Minio или File)
  PreviewService      ← PostgreSQL, StorageService
  PreviewStorage      ← PostgreSQL
  LogService          ← PostgreSQL
  KeyDerivationService ← PostgreSQL

Уровень 3 (бизнес-логика):
  SungeroWebServer    ← PostgreSQL, RabbitMQ, Elasticsearch, Storage
  SungeroWebClient    ← SungeroWebServer
  SungeroPublicApi    ← PostgreSQL, RabbitMQ
  IntegrationService  ← PostgreSQL, RabbitMQ
  IndexingService     ← PostgreSQL, Elasticsearch
  JobScheduler        ← PostgreSQL, RabbitMQ
  WorkflowBlockService ← PostgreSQL, RabbitMQ
  WorkflowProcessService ← PostgreSQL, RabbitMQ
  DelayedOperationsService ← PostgreSQL, RabbitMQ
  ReportService       ← PostgreSQL (Orleans)
  SungeroWidgets      ← PostgreSQL (Orleans)
  GenericService      ← PostgreSQL, RabbitMQ

Уровень 4 (вспомогательные):
  SungeroCentrifugo   ← WebServer (WebSocket push)
  ClientsConnectionService ← PostgreSQL
  SungeroWorker       ← PostgreSQL, RabbitMQ

Уровень 5 (инструменты, запускаются по требованию):
  DeploymentTool      ← PostgreSQL, WebServer
  DevelopmentStudioDesktop ← PostgreSQL, WebServer, Git
  InitialIndexing     ← PostgreSQL, Elasticsearch
  S3Tool              ← Minio
```

### 4.2 Docker Container Naming

```
{instance_name}_{service_name}

Пример (instance_name=directum):
  directum_postgres
  directum_rabbitmq
  directum_elasticsearch
  directum_haproxy
  directum_webserver
  directum_webclient
  directum_publicapi
  directum_jobscheduler
  directum_deploymenttool
  ...
```

---

## 5. Точки интеграции между компонентами

### 5.1 DDS ↔ DirectumLauncher

| Точка | Направление | Механизм |
|-------|-------------|----------|
| Установка DDS | Launcher → DDS | `do.sh ds install` → извлекает DDS archive, генерирует _ConfigSettings.xml |
| Запуск DDS | Launcher → DDS | `do.sh ds run` → запускает Electron + .NET backend |
| Конфигурация DDS | Launcher → DDS | `config.yml` → `DevelopmentStudioDesktop` секция → `_ConfigSettings.xml` |
| DevStand Config | DDS → WebServer | `devstand_config` секция config.yml → merge в WebServer config |
| VS Code Extension | Launcher → VS Code | `code --install-extension *.vsix` |

### 5.2 DDS ↔ DeploymentTool

| Точка | Направление | Механизм |
|-------|-------------|----------|
| Публикация из DDS | DDS → DTC | DDS вызывает DTC через HTTP API для deploy |
| Учётные данные | config.yml → оба | `DEPLOY_USERNAME/PASSWORD` (DDS) = `AUTHENTICATION_USERNAME/PASSWORD` (DTC) |
| Git-репозиторий | Общий | DDS пишет → DTC читает (export-package) |

### 5.3 DirectumLauncher ↔ DeploymentTool

| Точка | Направление | Механизм |
|-------|-------------|----------|
| Запуск DTC | Launcher → Docker | Docker container с volume mounts |
| CLI-команды | Launcher → DTC | Python wrapper → CLI args → DeploymentToolCore |
| Конфигурация | config.yml → DTC | LOGS_PATH, WEB_*, SERVER_*, AUTH_* |
| Результаты | DTC → Launcher | Exit codes (0-8) + stdout парсинг (regex) |
| Пути | Launcher → DTC | Трансформация host paths → container /app/ |

### 5.4 DeploymentTool ↔ Platform Services

| Точка | Направление | Механизм |
|-------|-------------|----------|
| Публикация сборок | DTC → WebServer | HTTP API → загрузка .dll |
| Схема БД | DTC → PostgreSQL | SQL DDL генерация → выполнение |
| Инициализация | DTC → WebServer → модули | HTTP request → ModuleInitializer.Execute() |
| Настройки | DTC → WebServer | HTTP request → ApplyDefaultSettings() |
| Health check | DTC → WebServer | HTTP GET /client/api/health |

---

## 6. CLI Quick Reference: Полный цикл

### Минимальный цикл (разработка)

```bash
# 1. Запустить инфраструктуру
do.sh all up

# 2. Запустить DDS (опционально)
do.sh ds run

# 3. Собрать .dat пакет (Claude Code / вручную)
#    Вариант A: через DTC export
do.sh dt export-package --export_package="Dev.dat" --root="/git" --repositories="work"
#    Вариант B: через zip (упрощённый, без компиляции)
cd source && zip -D -r ../Dev.dat PackageInfo.xml source/

# 4. Опубликовать
do.sh dt deploy --package="Dev.dat" --force

# 5. Проверить
do.sh dt get_deployed_solutions
do.sh all health
```

### Production цикл

```bash
# 1. Экспорт из git
do.sh dt export-package --export_package="Release.dat" \
  --root="/git" --repositories="work" --configuration="release.xml"

# 2. Merge нескольких пакетов (если нужно)
do.sh dt merge_packages "/output/Full.dat" \
  --packages="Base.dat;Custom.dat;Release.dat"

# 3. Distributed deploy (zero-downtime)
do.sh dt deploy --package="Full.dat" --distributed

# 4. Инициализация + настройки (если нужно)
do.sh dt init
do.sh dt import_settings --path="settings.datx"

# 5. Верификация
do.sh dt get_deployed_solutions
do.sh all health
```

### Диагностика

```bash
# Статус всех сервисов
do.sh all status

# Логи конкретного сервиса
do.sh webserver logs
do.sh jobscheduler logs

# Health check
do.sh all health

# Список опубликованных решений
do.sh dt get_deployed_solutions

# Версии сервисов
do.sh platform status
```

---

## 7. Карта портов

| Сервис | Порт | Протокол | Назначение |
|--------|------|----------|------------|
| HAProxy | 80/443 | HTTP/HTTPS | Точка входа, SSL termination, балансировка |
| SungeroWebServer | 8080 | HTTP | ASP.NET Core backend |
| SungeroPublicApi | 5000 | HTTP | OData REST API |
| PostgreSQL | 5432 | TCP | База данных |
| PGBouncer | 6432 | TCP | Connection pooling |
| RabbitMQ | 5672 | AMQP | Очередь сообщений |
| RabbitMQ Management | 15672 | HTTP | Web UI администрирования |
| Elasticsearch | 9200 | HTTP | Полнотекстовый поиск |
| Kibana | 5601 | HTTP | Визуализация логов |
| Minio | 9000/9001 | HTTP | S3-совместимое хранилище |
| Centrifugo | 8000 | WS | WebSocket push-уведомления |
| DevelopmentStudio | 7190 | HTTP | Backend DDS (Electron ↔ .NET) |

---

## 8. Форматы данных между компонентами

### 8.1 .dat пакет (ZIP)

```
Package.dat (ZIP archive)
├── PackageInfo.xml          ← XML: модули, версии, GUID
├── source/
│   └── <SolutionName>/
│       └── <ModuleName>/
│           ├── <EntityName>/
│           │   ├── <EntityName>.mtd          ← JSON: метаданные сущности
│           │   ├── <EntityName>Handlers.cs   ← C#: обработчики
│           │   ├── <EntityName>ClientHandlers.cs
│           │   ├── <EntityName>.resx         ← XML: локализация
│           │   └── <EntityName>_ru.resx
│           ├── Module/
│           │   ├── Module.mtd
│           │   ├── ModuleHandlers.cs
│           │   └── ModuleInitializer.cs
│           └── ...
└── settings/                ← Опционально: бизнес-настройки
```

### 8.2 .datx пакет (Settings Export)

```
Settings.datx (ZIP archive)
├── SettingsInfo.xml         ← Метаданные экспорта
└── data/
    └── [serialized settings objects]
```

### 8.3 _ConfigSettings.xml (DDS)

```xml
<?xml version="1.0" encoding="utf-8"?>
<ConfigSettings>
  <CompanyCode>Sungero</CompanyCode>
  <GitRootDirectory>/opt/directum/git_repository</GitRootDirectory>
  <Repositories>
    <Repository FolderName="work" SolutionType="Work" />
    <Repository FolderName="base" SolutionType="Base" />
  </Repositories>
  <DeployUsername>Administrator</DeployUsername>
  <DeployPassword>11111</DeployPassword>
  <WebRelativePath>Client</WebRelativePath>
  <WebProtocol>http</WebProtocol>
  <ServerHttpPort>80</ServerHttpPort>
  <ServerHttpsPort>443</ServerHttpsPort>
  <HelpUri>https://help.npo-comp.ru/DirectumRX/DS</HelpUri>
  <LogsPath>${basedir}/log</LogsPath>
</ConfigSettings>
```

---

## 9. Применение для Claude Code

### Что можно автоматизировать через Skills/MCP/Agents

| Операция | Текущий способ | Улучшенный способ |
|----------|---------------|-------------------|
| Сборка .dat | `zip -D -r` (упрощённый) | `do.sh dt export-package` (полный, с компиляцией) |
| Деплой | `deploy_to_stand` MCP tool | + `--force`, `--distributed`, exit-code parsing |
| Проверка деплоя | Ручной health check | `do.sh dt get_deployed_solutions` + `do.sh all health` |
| Диагностика | Чтение логов | `do.sh <service> logs` + `do.sh all status` |
| Merge пакетов | Нет | `do.sh dt merge_packages` |
| Settings | Нет | `do.sh dt export_settings` / `import_settings` |
| Версионирование | Ручное | `do.sh dt increment_version` / `set_version` |

### Связанные гайды
- [35_deployment_tool_internals.md](35_deployment_tool_internals.md) — DeploymentToolCore: все команды, флаги, exit-коды
- [36_launcher_internals.md](36_launcher_internals.md) — DirectumLauncher: архитектура, плагины, конфиг
- [37_development_studio_internals.md](37_development_studio_internals.md) — DevelopmentStudio: Electron+.NET, code gen, конфиг
- [34_applied_solution_packaging.md](34_applied_solution_packaging.md) — Формат .dat пакета, PackageInfo.xml
- [27_dds_vs_crossplatform_ds.md](27_dds_vs_crossplatform_ds.md) — DDS vs CrossPlatform DS
- [28_windows_autonomous_setup.md](28_windows_autonomous_setup.md) — Windows-стенд для автономной работы
