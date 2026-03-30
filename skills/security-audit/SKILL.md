---
description: "Аудит безопасности кода Directum RX — запрещённые паттерны, секреты, SQL-инъекции"
---

# Аудит безопасности Directum RX

**Этап 6 конвейера** `/pipeline`. Сканирует кодовую базу на запрещённые паттерны платформы и уязвимости.

## Что проверяется

### 1. Запрещённые паттерны Directum RX (КРИТИЧНО)

| Паттерн | Grep-regex | Замена | Severity |
|---------|-----------|--------|----------|
| is/as cast | `\b(is\|as)\s+I[A-Z]` | `Entities.Is()/As()` | CRITICAL |
| DateTime.Now/Today | `DateTime\.(Now\|Today)` | `Calendar.Now/Today` | CRITICAL |
| System.Threading | `System\.Threading` | AsyncHandlers | CRITICAL |
| System.Reflection | `System\.Reflection` | Запрещено | CRITICAL |
| Session.Execute | `Session\.Execute` | `SQL.CreateConnection()` | CRITICAL |
| new Tuple<> | `new\s+Tuple<` | Структуры `Create()` | HIGH |
| Анонимные типы | `new\s*\{[^}]*\w+\s*=` | Структуры | MEDIUM |
| GetAll() без Where | `GetAll\(\)\s*\.(Select\|ToList\|Count)` | Добавить Where | HIGH |
| Remote в Showing | Remote внутри Showing/Refresh | Params кэш | HIGH |

### 2. Захардкоженные секреты

Поиск в .cs файлах:
- `password\s*=\s*"[^"]+"` — пароли
- `apiKey\s*=\s*"[^"]+"` — API ключи
- `connectionString\s*=\s*"[^"]+"` — строки подключения
- `token\s*=\s*"[^"]+"` — токены
- `secret\s*=\s*"[^"]+"` — секреты

Severity: CRITICAL

### 3. SQL-инъекции

Поиск конкатенации строк в SQL:
- `CommandText\s*=.*\+` — конкатенация вместо параметров
- `string\.Format.*SELECT` без `SQL.AddParameter`
- `\$".*SELECT.*\{` — интерполяция в SQL

Правильно: `SQL.AddParameter(command, "@param", value, DbType.String)`

Severity: CRITICAL

### 4. Серверный код в клиенте / Клиентский на сервере

- `.ClientBase/` с `Create()`, `Delete()`, `SQL.` → CRITICAL
- `.Server/` с `Dialogs.`, `ShowMessage` → CRITICAL

### 5. Хардкод русских строк

- `"[а-яА-ЯёЁ]{3,}"` в .cs файлах (вне комментариев) → HIGH
- Правильно: `.resx` ресурсы

### 6. Права доступа

- Есть ли проверка прав перед операциями? (`AccessRights.CanRead/CanUpdate`)
- Не выданы ли избыточные права? (FullAccess вместо Read)
- Используется ли `GrantAccessRightsOnEntity` с конкретными правами?

### 7. Безопасность интеграций

- HTTPS для внешних вызовов?
- Таймауты установлены?
- Обработка ошибок внешних сервисов (не раскрывает внутренние детали)?
- Валидация входных данных от внешних систем?

## Workflow

1. Определи корень пакета (ищи `PackageInfo.xml` или `source/`)
2. Найди все .cs файлы через Glob `**/*.cs`
3. Для каждой категории — Grep по паттернам, исключая комментарии (`//`)
4. Собери findings с severity, file:line, описанием
5. Подсчитай Score: 100 - (CRITICAL * 25) - (HIGH * 10) - (MEDIUM * 3)
6. Выведи отчёт

## Формат вывода

```
=== Security Audit: {путь} ===

Score: {0-100}/100

CRITICAL:
- [CRITICAL] EntityHandlers.cs:42 — DateTime.Now → Calendar.Now
- [CRITICAL] ServerFunctions.cs:15 — entity as IEmployee → Employees.As(entity)

HIGH:
- [HIGH] ModuleFunctions.cs:89 — GetAll() без Where()

MEDIUM:
- [MEDIUM] Handlers.cs:23 — хардкод русской строки "Документ не найден"

Итого: 2 CRITICAL, 1 HIGH, 1 MEDIUM | Score: 37/100
```

## Следующий этап → `/validate-package`
## Предыдущий этап ← `/validate-all`

## Справочные материалы
- DDS known issues → CLAUDE.md