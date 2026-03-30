---
description: "Ревью кода Directum RX — качество, архитектура, паттерны платформы"
---

# Code Review для Directum RX

## MCP Tools (ОБЯЗАТЕЛЬНО используй)
- `analyze_solution action=health` — общий аудит решения: конфликты, сироты, дубликаты GUID, версии
- `check_code_consistency` — проверка согласованности MTD и C#: функции, классы, namespace, инициализатор
- `check_package` — валидация пакета: 8 проверок (коллекции, ссылки, enum, Code, resx, GUID)
- `check_resx` — найти неверные ключи System.resx (Resource_GUID вместо Property_Name)
- `check_permissions` — проверка AccessRights в MTD: пустые права, дубликаты, неизвестные роли
- `validate_guid_consistency` — cross-file GUID: Controls/Properties, resx Form_GUID/Forms, навигация
- `validate_workflow` — валидация RouteScheme: мёртвые блоки, тупики, переходы без условий
- `validate_report` — валидация отчёта: .frx и Queries.xml, датасеты, подключения
- `dependency_graph action=cycles` — проверка на циклические зависимости между модулями

**Этап 4 конвейера** `/pipeline`. Комплексное ревью кода с учётом специфики платформы Directum RX.

## Что проверяется

### 1. Запрещённые паттерны платформы
Все паттерны из CLAUDE.md (is/as, DateTime, Threading, Reflection, Session.Execute и т.д.)

### 2. Архитектура Server/Client/Shared

| Нарушение | Severity | Описание |
|-----------|----------|----------|
| CRUD в Client | CRITICAL | `Create()`, `Delete()`, `SQL.` в ClientBase/ |
| Диалоги в Server | CRITICAL | `Dialogs.`, `ShowMessage` в Server/ |
| Remote в Showing/Refresh | HIGH | Remote-вызовы в часто вызываемых событиях |
| Бизнес-логика в Shared | MEDIUM | Shared только для вычислений |
| Отсутствие base.Event() | HIGH | `base.BeforeSave(e)` и т.д. в override |

### 3. Метрики кода

| Метрика | OK | WARN | FAIL |
|---------|-----|------|------|
| Длина метода | ≤30 | 31-50 | >50 |
| Цикломатическая сложность | ≤10 | 11-20 | >20 |
| Вложенность | ≤3 | 4-5 | >5 |
| Параметры метода | ≤4 | 5-6 | >6 |

### 4. Специфика Directum RX

- **Блокировки**: `Locks.TryLock()` перед изменением в async/job?
- **Retry**: `args.Retry = true` при неудачной блокировке в AsyncHandler?
- **Транзакции**: `Transactions.Execute()` в Job при обработке списков?
- **base.Event()**: Вызывается в override-методах?
- **Save()**: Вызывается после изменений в Job/AsyncHandler?
- **Logger**: Используется для отладки?
- **Ресурсы**: Строки через `.resx`, не хардкод?
- **Entities.Is/As**: Вместо is/as для NHibernate прокси?
- **Calendar.Now**: Вместо DateTime.Now?

### 5. Производительность

- `GetAll()` без `Where()` → FAIL
- `ToList()` на больших выборках → WARN
- Вложенные циклы с `GetAll()` → FAIL (N+1)
- Remote в Showing/Refresh → FAIL
- Много навигационных свойств в Where → WARN

### 6. Паттерны кода (соответствие base)

- Using statements корректны для каждого типа файла?
- Namespace соответствует директории?
- Обработчики используют стандартные сигнатуры?
- BlockHandlers в правильном namespace?

### 7. Production-паттерны для сверки (Targets/OmniApplied)

**AsyncHandlers:**
- ✅ `args.Retry` / `args.NextRetryTime` / `Locks.TryLock` — не голый catch
- ✅ Batch: processLimit + retry пока есть необработанные
- ❌ `catch { Logger.Error(); }` без retry — данные остаются несогласованными

**WebAPI:**
- ✅ `ICommonResponse` (IsSuccess + Message) — типизированный ответ
- ❌ `return jsonString` — нетипизированный, сложно отлаживать

**Initializer:**
- ✅ `ModuleVersionInit` — версионная инициализация
- ✅ `ExternalLink` для предопределенных записей
- ❌ Вся логика в одном `Initializing()` — дубликаты при обновлении

**Override:**
- ✅ `base.Method()` первой строкой в override
- ❌ Override без base.Method() — ломает цепочку

**Reference:**
- `targets/CODE_PATTERNS_CATALOG.md`
- `omniapplied/REFERENCE_CATALOG.md`

### 8. MTD проверка

- Все PropertyGuid уникальны?
- BaseGuid корректен для типа сущности?
- $type корректен?
- Forms/Controls синхронизированы с Properties?
- `DomainApi:2` в Versions каждой сущности?
- FilterPanel.NameGuid фиксированный (DatabookEntry=`b0125fbd`, Document=`80d3ce1a`, Task=`bd0a4ce3`, Assignment=`23d98c0f`, Notice=`8b3cedfe`)?
- Document Card Form NameGuid = `fa03f748-...` с `IsAncestorMetadata: true`?
- Assignment Result.Overridden = `["Values"]` (НЕ `["DirectValues"]`!)?
- Assignment AttachmentGroups = те же NameGuid что в Task + `IsAssociatedEntityGroup: true`?
- Assignment Ribbon кнопки → ParentGuid = `ac82503a-...` (AsgGroup), Groups: []?
- IsSolutionModule зависимость в Module.mtd с правильным GUID решения?

### 9. DDS-совместимость .cs файлов (КРИТИЧНО)

| Нарушение | Severity | Описание |
|-----------|----------|----------|
| `public class` вместо `partial class` | CRITICAL | DDS генерирует partial-классы, `public class` = конфликт |
| ModuleInitializer с базовым классом | CRITICAL | `: Sungero.Domain.ModuleInitializer` — DDS генерирует сам |
| Свойства в Structures.cs | CRITICAL | DDS генерирует из PublicStructures в .mtd |
| Неправильное имя Handler-класса | HIGH | Server=`ModuleServerHandlers`, Client=`ModuleClientHandlers` |
| .resx version=1.3 или Version=2.0.0.0 | CRITICAL | Должно быть 2.0 и 4.0.0.0 |

## Workflow

### Шаг 0: MCP автоматические проверки (СНАЧАЛА запусти MCP)

```
MCP: analyze_solution action=health solutionPath={путь_к_решению}
MCP: check_code_consistency packagePath={путь_к_пакету}
MCP: check_package packagePath={путь_к_пакету}
MCP: check_resx directoryPath={путь_к_пакету}
MCP: check_permissions path={путь_к_пакету}
MCP: validate_guid_consistency modulePath={путь_к_модулю}
MCP: dependency_graph action=cycles
```
Если есть задачи — также:
```
MCP: validate_workflow path={путь_к_Task.mtd}
```
Если есть отчёты — также:
```
MCP: validate_report path={путь_к_отчёту}
```
Результаты MCP включи в findings. Затем переходи к ручной проверке.

### Шаг 1-7: Ручная проверка

1. Определи область ревью (указанные файлы или весь пакет)
2. Прочитай каждый .cs файл
3. Прочитай каждый .mtd файл
4. Проверь все категории
5. Собери findings (объедини MCP-результаты и ручные)
6. Рассчитай Score: 100 - penalties
7. Выведи вердикт

## Scoring

| Penalty | Severity |
|---------|----------|
| -25 | CRITICAL |
| -10 | HIGH |
| -3 | MEDIUM |
| -1 | LOW |

| Score | Вердикт |
|-------|---------|
| 90-100 | PASS — код готов |
| 70-89 | CONCERNS — есть замечания |
| <70 | ISSUES — нужны исправления |

## Формат вывода

```
=== Code Review: {область} ===

Score: {score}/100 | Verdict: {PASS|CONCERNS|ISSUES}

CRITICAL ({N}):
- EntityHandlers.cs:42 — DateTime.Now → Calendar.Now

HIGH ({N}):
- ModuleFunctions.cs:15 — GetAll() без Where()

MEDIUM ({N}):
- ServerFunctions.cs:67 — метод 62 строки

LOW ({N}):
- Constants.cs — неиспользуемая константа

Рекомендации:
1. ...
```

## Следующий этап → `/validate-all`
## Предыдущий этап ← `/pipeline` (фаза Implementation)

## Справочные материалы
- DDS known issues → CLAUDE.md
- Эталонный код: платформенные модули (внутри контейнеров RX), MCP: `search_metadata` / `extract_entity_schema`