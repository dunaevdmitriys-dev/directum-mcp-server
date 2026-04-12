# Directum RX 26.1 — Рабочее пространство

Полная автономность. Язык — русский.
Детальные правила, запреты, Known Issues → `.claude/rules/core.md` (всегда в контексте).

---

## Инфраструктура

### VPS (Production)
<!-- Заполни своими данными. Удали секцию если VPS нет. -->
- **IP**: <YOUR_VPS_IP>
- **SSH**: `ssh -4 -i <YOUR_SSH_KEY_PATH> root@<YOUR_VPS_IP>`
- **Web Client**: http://<YOUR_VPS_IP>:8080/Client/
- **CRM SPA**: http://<YOUR_VPS_IP>:8080/Client/content/crm/
- **OData**: http://<YOUR_VPS_IP>:8080/Integration/odata/
- **CRM API**: http://<YOUR_VPS_IP>:5099 (Swagger: /swagger)

### Docker Services (deploy/docker-compose.*.yml)

| Сервис | Контейнер | Порт |
|--------|-----------|------|
| PostgreSQL 15.3 | directum-postgres | 5432 |
| RabbitMQ 4.0 | directum-rabbitmq | 5672 / 15672 |
| Elasticsearch 7.17 | directum-elasticsearch | 9200 |
| HAProxy | directum-haproxy | 8080 |
| WebServer | sungerowebserver_directum | 44310 |
| WebClient | — | 44320 |
| StorageService | — | 44330 |
| IntegrationService | — | 44340 |
| Grains | sungerograins_directum | — |
| Worker | sungeroworker_directum | — |
| ProcessService | sungeroprocessservice_directum | — |
| DelayedOps | sungerodelayedops_directum | — |

```bash
docker compose -f deploy/docker-compose.infra.yml up -d   # PostgreSQL, RabbitMQ, ES
docker compose -f deploy/docker-compose.rx.yml up -d       # Directum RX (8 сервисов)
```

### Credentials
<!-- Заполни своими данными. НЕ коммить в публичный репо! -->
- **RX**: <YOUR_RX_USERNAME> / <YOUR_RX_PASSWORD>
- **PostgreSQL**: <YOUR_PG_USER> / <YOUR_PG_PASSWORD> (port 5432)
- **RabbitMQ**: <YOUR_RMQ_USER> / <YOUR_RMQ_PASSWORD> (port 5672)
- **GitHub PAT**: <YOUR_GITHUB_PAT>

### GitHub
<!-- Заполни своими данными -->

| Репо | Видимость | Назначение |
|------|-----------|-----------|
| <your-org>/directum-mcp-server | Public | MCP-инструменты для DDS |
| <your-org>/<your-project> | Private | Пакет разработки |

---

## Проект — быстрая справка
<!-- Опиши свой проект. Пример ниже — CRM, замени на свой. -->

Описание проекта: <YOUR_PROJECT_DESCRIPTION>
Подробности → `<YOUR_PROJECT_DIR>/CLAUDE.md`.

| Компонент | Локально | На VPS |
|-----------|----------|--------|
| Directum RX | :8080 (docker) | :8080 (docker) |
| OData | :8080/Integration/odata | :8080/Integration/odata |

### DDS Deploy Pipeline
```bash
# Локально: собрать и залить
cd <YOUR_PACKAGE_DIR> && zip -r Package.dat PackageInfo.xml source/
scp Package.dat root@<YOUR_VPS_IP>:/opt/directum/Package.dat

# На VPS: export + deploy
cd /opt/directum/dt
./DeploymentToolCore -e /opt/directum/Package-compiled.dat --root /opt/directum/git --repositories base --work work
./DeploymentToolCore -n <YOUR_RX_USERNAME> -p <YOUR_RX_PASSWORD> -d /opt/directum/Package-compiled.dat
```

### Ключевые пути на VPS
<!-- Адаптируй под свой стенд -->
- Compose: `/opt/directum/docker-compose.{infra,rx}.yml`
- HAProxy: `/opt/directum/home/haproxy/haproxy.cfg`
- DT binary: `/opt/directum/dt/DeploymentToolCore`
- Git repos: `/opt/directum/git/{base,work}/`
- Logs: `/opt/directum/home/logs/`

---

## MCP Tools — диспетчерская таблица

| Задача | MCP tool |
|--------|----------|
| Найти сущность по имени | `search_metadata name=...` |
| Извлечь схему сущности | `extract_entity_schema entity=...` |
| Предсказать OData имя | `predict_odata_name entity=...` |
| Создать сущность (.mtd) | `scaffold_entity`, `scaffold_function`, `scaffold_dialog` |
| Валидация после изменений | `validate_all`, `check_package`, `check_code_consistency` |
| Проверить .resx | `check_resx`, `find_dead_resources`, `sync_resx_keys` |
| Проверить GUID | `validate_guid_consistency` |
| Собрать .dat пакет | `build_dat` |
| Деплой на стенд | `deploy_to_stand` |
| Диагностика ошибки сборки | `diagnose_build_error` |
| Граф зависимостей | `dependency_graph`, `visualize_dependencies` |
| Анализ решения | `analyze_solution`, `solution_health` |

**MCP Servers** (конфиг: `.mcp.json`):
- **directum-dev** (89 tools): scaffold, validate, analyze, build, deploy — `.claude/rules/mcp-devtools.md`

---

## C# — конвенции Sungero

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Sungero.Core;
using Sungero.CoreEntities;
using Sungero.Domain.Shared;        // для Enumeration
using {ModuleNamespace}.{EntityName}; // для свойств сущности
```
- Все классы — `partial` (кроме Constants)
- Namespace: `{Company}.{Module}.Server` / `.Client` / `.Shared`
- ServerFunctions: `public virtual` / `public static`
- SharedFunctions: `public virtual`
- SQL: ТОЛЬКО параметризованные запросы (NpgsqlParameter)
- WebAPI ответы: OData `{"value":"..."}` → `UnwrapODataValue()`

## Структура .mtd файла

```
NameGuid        — уникальный ID сущности (из extract_entity_schema)
Name            — PascalCase имя
BaseGuid        — GUID родительской сущности
Properties[]    — свойства (NameGuid, Name, IsRequired, InternalDataTypeName)
Actions[]       — действия (NameGuid, Name, GenerateHandler)
RibbonCardMetadata/RibbonCollectionMetadata — кнопки на карточке/списке
```
Типы: `System.Int32`, `System.String`, `System.DateTime`, `System.Boolean`, `System.Double`, `Sungero.Domain.Shared.EnumerationItem`

---

## Маршрутизация правил и справочников

| Что | Где |
|-----|-----|
| Запреты, алгоритм, приоритеты, Known Issues | `.claude/rules/core.md` |
| Разработка vs Веб-настройка | `.claude/rules/core.md` |
| MCP DevTools правила | `.claude/rules/mcp-devtools.md` |
| Deploy правила | `.claude/rules/deploy.md` |
| .mtd шаблоны | `knowledge-base/guides/23_mtd_reference.md` |
| C# паттерны | `knowledge-base/guides/25_code_patterns.md` |
| Known Issues (22 шт.) | `docs/platform/DDS_KNOWN_ISSUES.md` |
| Каталог всех гайдов | `knowledge-base/INDEX.md` |
| Справка RX | `дистрибутив/webhelp/` |

---

## Как начать

1. Склонируй MCP-сервер: `git clone https://github.com/dunaevdmitriys-dev/directum-mcp-server.git`
2. Скопируй этот файл: `cp CLAUDE-TEMPLATE.md ~/your-project/CLAUDE.md`
3. Заполни `<YOUR_...>` плейс��олдеры своими данными
4. Скопируй `.mcp.json` и укажи `SOLUTION_PATH` на свой workspace
5. Скопируй `rules/` и `skills/` в `.claude/` своего проекта
6. Запусти: `scripts/setup.sh` (установит .NET 10, проверит зависимости)
7. Проверь: `/validate-all` — должно пройти без ошибок
