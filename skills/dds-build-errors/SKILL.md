---
description: "Диагностика ошибок сборки DDS: Missing area, NullReferenceException, file locks, reserved identifiers, SourceTableName, override, WebAPI, AsyncHandler. Используй при ЛЮБОЙ ошибке DDS build."
---

# DDS Build Errors — Справочник ошибок и решений

**Используй когда DDS сборка/импорт падает.** НЕ гадай причину — найди ошибку в таблице ниже.

## КРИТИЧЕСКОЕ ПРАВИЛО: Разрыв цикла ошибок

```
СТОП. Ты уже пробовал исправить эту ошибку?
├── ДА, тем же способом → СТОП. Другой подход. Читай таблицу ниже.
├── ДА, другим способом → Запусти /validate-all. Возможно есть ДРУГАЯ ошибка.
└── НЕТ → Найди ошибку в таблице, примени fix, запусти /validate-all.
```

## Таблица ошибок

### A. Ошибки импорта .dat

| Ошибка | Причина | Fix |
|--------|---------|-----|
| "Missing area" в BaseGenerator | CollectionPropertyMetadata в DatabookEntry | Удалить коллекции ИЛИ сменить на Document тип |
| NullReferenceException в InterfacesGenerator | CollectionPropertyMetadata в DatabookEntry | То же |
| "Missing dependency" | NavigationProperty ссылается на модуль вне Dependencies | Добавить модуль в Dependencies Module.mtd |
| Circular dependency | Модуль A → B → A | Вынести общий тип в модуль C |
| File lock на .csproj | dotnet.exe не отпустил файл после предыдущей сборки | Перезапустить DDS |
| "Type not found" | EntityGuid ссылается на несуществующую сущность | Проверить GUID в MCP: validate_guid_consistency |
| "Metadata conflict in LayerModule" | BaseGuid != LayeredFromGuid | Проверь: omniapplied/REFERENCE_CATALOG.md секция 10 |
| "Unknown module in Dependencies" | LayerModule AssociatedGuid должен указывать на SolutionMetadata | Проверить AssociatedGuid в Module.mtd → SolutionMetadata |

### B. Ошибки компиляции

| Ошибка | Причина | Fix |
|--------|---------|-----|
| CS0101 duplicate class | Два файла определяют один класс, не partial | Добавить `partial` keyword |
| CS0246 type not found | Отсутствует using или зависимость | Проверить Dependencies в Module.mtd |
| CS0234 namespace not exist | Неправильный namespace в .cs | Server/ → `.Server`, ClientBase/ → `.Client` (НЕ `.ClientBase`!) |
| Reserved word as identifier | Enum value `New`, `Default` и т.д. | Переименовать: `NewDeal`, `DefaultPriority` |
| SourceTableName missing | Report без SourceTableName в .mtd | Добавить SourceTableName в ReportMetadata |
| IsDisplayValue missing | Нет строкового свойства с IsDisplayValue: true | Добавить IsDisplayValue: true к одному StringPropertyMetadata |
| "base.Method() not called" | override-методы ОБЯЗАНЫ вызывать base | Добавить вызов base.Method(). Reference: omniapplied |

### C. Ошибки runtime (после публикации)

| Ошибка | Причина | Fix |
|--------|---------|-----|
| Пустые подписи полей | Resource_<GUID> вместо Property_<Name> в System.resx | Заменить ключи. Пересобрать satellite DLL |
| "Can't resolve function" | FunctionName в MTD ≠ метод в ClientFunctions.cs | Привести к точному совпадению |
| Модуль без названия | Нет DisplayName в ModuleSystem.ru.resx | Добавить DisplayName |
| Пустые заголовки списков | Нет CollectionDisplayName в EntitySystem.ru.resx | Добавить CollectionDisplayName |
| Satellite DLL отсутствует | Не скопирована в AppliedModules/ru/ | Пересобрать: ResourceWriter → al.exe → копировать |
| "WebAPI endpoint returns 404" | PublicAPI не перезапущен / endpoint не зарегистрирован | Перезапустить PublicAPI. Проверить регистрацию endpoint в Module.mtd |
| "AsyncHandler runs infinitely" | Нет условия выхода из retry (нет args.Retry = false или MaxRetryCount) | Добавить args.Retry = false или установить MaxRetryCount |

### D. Ошибки DDS UI

| Ошибка | Причина | Fix |
|--------|---------|-----|
| Пустая карточка | `Overridden: ["Controls"]` + `Controls: []` | Убрать Controls из Overridden ИЛИ заполнить Controls |
| Вкладки не появляются | FormTabs в .mtd | Не поддерживается в DDS 26.1 (StandaloneFormMetadata). Убрать FormTabs |
| Библиотека исчезла | Добавлена через .csproj, не через DDS UI | Добавить через DDS: Сторонние библиотеки → + |
| Структура без свойств | Свойства в .cs вместо Module.mtd | Определить в Module.mtd → PublicStructures → Properties |

## Алгоритм диагностики

```
1. Прочитай ПОЛНЫЙ текст ошибки (не только первую строку!)
2. Найди ошибку в таблице выше
3. Если не нашёл → запусти MCP: trace_errors для полных логов
4. Примени fix
5. Запусти /validate-all
6. Если ошибка повторилась → ДРУГАЯ причина. Перечитай полный текст.
```

## Пересборка satellite DLL
См. CLAUDE.md пункт 12 (пошаговая инструкция al.exe + Stop-Service).

## E. Ошибки DeploymentToolCore (exit-коды)

При деплое через `do.sh dt deploy` DTC возвращает exit-код:

| Exit-код | Значение | Диагностика |
|----------|----------|-------------|
| 0 | Успех | — |
| 1 | Pre-deploy error | Невалидные аргументы, corrupt .dat, ошибка генерации PackageInfo |
| 2 | Deploy error | WebServer недоступен, сеть, несовпадение версий DTC ≠ WebServer (release level) |
| 3 | Init error | ModuleInitializer упал, проверь логи: `do.sh dt logs` |
| 4 | Settings error | Не удалось применить default settings |
| 5 | Import settings error | Corrupt .datx файл |
| 7 | Export package error | Corrupt исходники в git (битые .cs, отсутствующие .mtd) |
| 8 | Version error | increment_version / set_version failed |

### Диагностика по exit-коду

```bash
LAUNCHER="$WORKSPACE/дистрибутив/launcher"

# Проверить последний exit-код
echo $?

# Прочитать логи DTC
grep -iE "ERROR|FAIL|Exception" $LAUNCHER/log/current.log

# Проверить версию DTC vs WebServer
$LAUNCHER/do.sh dt get_deployed_solutions

# Проверить что WebServer доступен
$LAUNCHER/do.sh all health
```

## Если ничего из таблицы не подходит

1. MCP: `trace_errors` — полные логи
2. MCP: `analyze_solution action=health` — состояние решения
3. `do.sh all status` — проверить что все сервисы запущены
4. Сделать бэкап ПЕРЕД экспериментами
5. Спросить пользователя — возможно проблема окружения

## Ссылки
- `knowledge-base/guides/35_deployment_tool_internals.md` — полная reference DTC
- `knowledge-base/guides/38_platform_integration_map.md` — карта интеграций
