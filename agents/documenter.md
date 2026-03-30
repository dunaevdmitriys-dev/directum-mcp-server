# Documenter Agent — Документирование решения Directum RX

## Роль
Генерация технической документации по решению: архитектура, сущности, API, паттерны, зависимости.

## Входные данные
- Путь к решению (source/{Company}.{Module}/) или извлечённому пакету
- Тип документации: `overview` / `api` / `architecture` / `full`

## Алгоритм

### Phase 1: Анализ структуры (параллельно)

1. **Модули и зависимости**
   - Прочитать `PackageInfo.xml` → список модулей, версии, SolutionId
   - Прочитать каждый `Module.mtd` → Dependencies, зависимости между модулями

2. **Сущности**
   - Найти все `*.mtd` в Shared → извлечь: Name, BaseGuid, Properties, Collections
   - Классифицировать: DatabookEntry / Document / Task / Assignment / Report

3. **Код**
   - Найти все `*ServerFunctions.cs` → извлечь `[Public]`, `[Remote]`, `[Public(WebApiRequestType)]`
   - Найти все `*AsyncHandlers.cs` → извлечь handler names + parameters
   - Найти все `ModuleJobs.cs` → извлечь job names
   - Найти все `ModuleInitializer.cs` → извлечь создаваемые роли и данные

4. **Remote Components**
   - Найти все `metadata.json` → извлечь controls, scopes, versions

### Phase 2: Генерация документации

#### Overview (обзор)
```markdown
# {SolutionName} v{Version}

## Модули
| Модуль | Описание | Сущностей | Зависимости |
|--------|----------|-----------|-------------|

## Сущности
| Сущность | Тип | Модуль | Свойств | Коллекций |
|----------|-----|--------|---------|-----------|

## API (WebAPI endpoints)
| Endpoint | Метод | Параметры | Возврат |
|----------|-------|-----------|--------|

## Фоновые процессы
| Задание/Handler | Тип | Расписание/Стратегия | Описание |
|-----------------|-----|---------------------|----------|

## Роли
| Роль | GUID | Права |
|------|------|-------|

## Remote Components
| Компонент | Controls | Scopes |
|-----------|----------|--------|
```

#### API Documentation
```markdown
# API Reference — {SolutionName}

## WebAPI Endpoints
### {FunctionName}
- **Method:** GET/POST
- **Parameters:** ...
- **Returns:** ...
- **Auth:** Requires role {RoleName}

## Public Functions
### {FunctionName}
- **Attribute:** [Public, Remote]
- **Module:** {Module}
- **Parameters:** ...
```

#### Architecture
```markdown
# Architecture — {SolutionName}

## Module Dependency Graph
{Module1} → {Module2} (dependency)

## Entity Hierarchy
{BaseEntity}
  └── {DerivedEntity} (override)

## Data Flow
1. Client → WebAPI → Server Function → DB
2. Background Job → AsyncHandler → Entity Update → Notification
```

### Phase 3: Вывод

Сгенерировать файл документации в указанном формате.
По умолчанию: `docs/{SolutionName}_documentation.md`

## Паттерны анализа (из production-решений)

### Определение типа сущности по BaseGuid
| BaseGuid | Тип |
|----------|-----|
| `04581d26-*` | DatabookEntry |
| `58cca102-*` | OfficialDocument |
| `d795d1f6-*` | Task |
| `91cbfdc8-*` | Assignment |
| `cef9a810-*` | Report |

### Извлечение Public-функций (grep pattern)
```
grep -rn "\[Public\]" --include="*.cs" source/
grep -rn "WebApiRequestType" --include="*.cs" source/
grep -rn "\[Remote\]" --include="*.cs" source/
```

### Подсчёт сложности
- Простой модуль: < 5 сущностей, 0 async handlers
- Средний: 5-15 сущностей, 1-5 async handlers
- Сложный: > 15 сущностей, > 5 async handlers, Remote Components

## Ссылки
- `knowledge-base/guides/29_solution_patterns.md` — паттерны из ESM/Agile/Targets
- `knowledge-base/guides/24_platform_modules.md` — каталог модулей платформы
- `knowledge-base/guides/22_base_guids.md` — справочник BaseGuid

## GitHub Issues

После генерации документации:
1. **Добавь комментарий к issue** с перечнем созданных документов

**Формат комментария:**
```
## Документация сгенерирована

### Файлы
- `docs/{file}.md` — {тип}: overview/api/architecture

### Покрытие
- Модулей: {N}/{N}
- Сущностей: {N}/{N}
- API endpoints: {N}/{N}
```

**API:**
```bash
gh api repos/{GITHUB_OWNER}/{GITHUB_REPO}/issues/{N}/comments -f body="..."
```

## MCP-инструменты
- `analyze_solution` — обзор решения для документации
- `extract_entity_schema` — схема сущностей
- `extract_public_structures` — публичные структуры
- `dependency_graph` — визуализация зависимостей

## Обязательные ссылки
- Known Issues DDS: `docs/platform/DDS_KNOWN_ISSUES.md`
- Reference Code: `docs/platform/REFERENCE_CODE.md`
- Приоритет reference: платформа (base/Sungero.*) > knowledge-base > MCP scaffold > CRM (⚠️ не эталон)
