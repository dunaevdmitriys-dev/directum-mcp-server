---
description: "Валидация пакета разработки Directum RX — финальная проверка перед импортом"
---

# Валидация пакета Directum RX

**Этап 7 конвейера** `/pipeline`. Финальная проверка пакета перед импортом в DDS.

## Использование
Запусти `/validate-package` и укажи путь к пакету (или он будет определён автоматически из `{ProjectName}/source/`).

## Что проверяется

### A. Запрещённые паттерны в .cs файлах

Найди все .cs файлы и проверь отсутствие:

| Паттерн | Regex | Правильная замена |
|---------|-------|------------------|
| `is` cast | `\bentity\s+(is)\s+I[A-Z]` | `Entities.Is(entity)` |
| `as` cast | `\bas\s+I[A-Z]` | `Entities.As(entity)` |
| DateTime.Now | `DateTime\.(Now\|Today)` | `Calendar.Now/Today` |
| System.Threading | `System\.Threading` | AsyncHandlers |
| System.Reflection | `System\.Reflection` | Запрещено |
| Session.Execute | `Session\.Execute` | `SQL.CreateConnection()` |
| new Tuple | `new\s+Tuple<` | Структуры через `Create()` |
| Анонимные типы | `new\s*\{[^}]*=` (в server/client) | Структуры |

### B. Структура файлов

Для каждого модуля в `source/`:
- [ ] Существует `{Module}.Server/`, `{Module}.ClientBase/`, `{Module}.Shared/`
- [ ] В Shared/ есть `Module.mtd`, `Module.resx`, `Module.ru.resx`
- [ ] Для каждой сущности:
  - [ ] `{Entity}.mtd` в Shared/{Entity}/
  - [ ] `{Entity}.resx` + `{Entity}.ru.resx`
  - [ ] `{Entity}Handlers.cs` в Server/{Entity}/
  - [ ] `{Entity}Handlers.cs` в ClientBase/{Entity}/

### C. JSON валидность .mtd файлов

Все .mtd — валидный JSON. Проверь:
```bash
python3 -m json.tool file.mtd > /dev/null
```

### D. GUID уникальность

Извлеки все `"NameGuid"` из всех .mtd. Проверь, что нет дубликатов.

### E. .resx парность

Каждый `.resx` должен иметь парный `.ru.resx`.

### F. Namespace соответствие

В каждом .cs файле namespace должен соответствовать директории:
- `{Module}.Server/` → `namespace {Company}.{Module}.Server`
- `{Module}.ClientBase/` → `namespace {Company}.{Module}.Client` (НЕ ClientBase!)
- `{Module}.Shared/` → `namespace {Company}.{Module}` или `{Company}.{Module}.Shared`

### G. PackageInfo.xml

- Все модули из `source/` должны быть перечислены в PackageInfo.xml
- GUID в PackageInfo = NameGuid из Module.mtd

### H. settings/Module.json

- Для каждого модуля есть `settings/{Module}/Module.json`
- NameGuid совпадает с Module.mtd

### I. Кросс-файловая проверка

- Каждое свойство в Entity.mtd Properties должно иметь контрол в Forms.Controls
- Properties с `IsRequired: true` → контрол есть на форме
- BaseGuid корректен
- $type корректен

### J. DDS-совместимость (КРИТИЧНО)

Проверки, предотвращающие ошибки при импорте/сборке в DDS:

#### J1. Все .cs классы — partial (кроме Constants)
```bash
# Найти все .cs файлы с `public class` (НЕ partial) — это ошибка
grep -rn "public class Module" --include="*.cs" source/
```
Исключения: только `ModuleConstants.cs` (`public static class Module`).
Все остальные: `partial class` или `public partial class`.

**Имена классов:**
- ModuleServerFunctions.cs → `partial class ModuleFunctions`
- ModuleHandlers.cs (Server) → `partial class ModuleServerHandlers`
- ModuleHandlers.cs (Client) → `partial class ModuleClientHandlers`
- ModuleInitializer.cs → `public partial class ModuleInitializer` (БЕЗ базового класса!)
- EntityHandlers.cs → `partial class {Entity}Handlers`
- EntityServerFunctions.cs → `partial class {Entity}Functions`
- EntityActions.cs → `partial class {Entity}Actions`

#### J2. ModuleInitializer — без базового класса
```bash
# Ошибка: `: Sungero.Domain.ModuleInitializer`
grep -rn "class ModuleInitializer\s*:" --include="*.cs" source/
```
DDS генерирует базовый класс сам.

#### J3. Structures — без свойств
Если структура определена в Module.mtd `PublicStructures`, .cs файл должен быть пустым partial class.

#### J4. IsSolutionModule зависимость
В Module.mtd Dependencies ОБЯЗАНА быть запись с `"IsSolutionModule": true` и GUID решения из PackageInfo.xml.

#### J5. .resx заголовки
```bash
# Проверить version=2.0 и Version=4.0.0.0
grep -l "Version=2.0.0.0" --include="*.resx" -r source/  # ← ОШИБКА если найдены
grep -l "<value>1.3</value>" --include="*.resx" -r source/  # ← ОШИБКА если найдены
```
Правильно: version=`2.0`, reader/writer=`Version=4.0.0.0`.

#### J6. DomainApi:2 в Versions
Каждый .mtd сущности ОБЯЗАН иметь `{ "Type": "DomainApi", "Number": 2 }` в Versions.

#### J7. FilterPanel фиксированные GUID
| Базовый тип | FilterPanel.NameGuid |
|-------------|----------------------|
| DatabookEntry | `b0125fbd-3b91-4dbb-914a-689276216404` |
| Document | `80d3ce1a-9a72-443a-8b6c-6c6eef0c8d0f` |
| Task | `bd0a4ce3-3467-48ad-b905-3820bf6b9da6` |
| Assignment | `23d98c0f-b348-479d-b1fb-ccdcf2096bd2` |
| Notice | `8b3cedfe-01e2-47a9-b77d-3a7d6ad7904f` |

#### J8. Document Card Form GUID
Документы ОБЯЗАНЫ использовать `fa03f748-4397-42ef-bdc2-22119af7bf7f` с `IsAncestorMetadata: true`.

#### J9. Assignment Result.Overridden
`Overridden: ["Values"]` (НЕ `["DirectValues"]`!). Каждый DirectValue — с `"Versions": []`.

#### J10. Assignment AttachmentGroups
Используют ТЕ ЖЕ NameGuid что и в Task + `IsAssociatedEntityGroup: true`, `IsAutoGenerated: true`.

#### J11. Assignment Ribbon AsgGroup
Кнопки → `ParentGuid: "ac82503a-7a47-49d0-b90c-9bb512c4559c"`. НЕ переопределять группу в Groups[].

#### J12. CollectionPropertyMetadata в DatabookEntry (КРИТИЧНО)
DatabookEntry-сущности НЕ ДОЛЖНЫ иметь `CollectionPropertyMetadata` (дочерние коллекции) при импорте через .dat. Это вызывает:
- "Missing area" в BaseGenerator
- NullReferenceException в InterfacesGenerator.Generate

```bash
# Найти DatabookEntry с CollectionPropertyMetadata
grep -l '"CollectionPropertyMetadata"' source/**/*.mtd | while read f; do
  grep -q '"BaseGuid": "04581d26' "$f" && echo "FAIL: $f — DatabookEntry с CollectionPropertyMetadata"
done
```
Решение: использовать Document-тип или удалить коллекции перед импортом.

#### J13. Кросс-модульные ссылки NavigationProperty
Все `EntityGuid` в NavigationPropertyMetadata должны ссылаться на сущности из модулей, перечисленных в Dependencies Module.mtd. Циклические зависимости запрещены.

```bash
# Извлечь все EntityGuid из NavigationPropertyMetadata и проверить,
# что каждый GUID найден в .mtd файлах модулей из Dependencies
```

#### J14. Зарезервированные слова C# в enum-значениях
Enum Name/Code НЕ ДОЛЖНЫ быть зарезервированными словами C#: `new`, `event`, `class`, `public`, `private`, `return`, `void`, `string`, `int`, `bool`, `object`, `base`, `this`, `null`, `true`, `false`, `default`, `is`, `as`, `in`, `out`, `ref`, `var`, `typeof` и др.

```bash
# Проверить DirectValues в .mtd на зарезервированные слова
grep -rn '"Name": "New"\|"Name": "Event"\|"Name": "Class"\|"Name": "Base"\|"Name": "String"\|"Name": "Default"' --include="*.mtd" source/
```

#### J15. Уникальность Code свойств в иерархии наследования
Свойства с одинаковым `Code` в сущностях одной иерархии наследования создают дублирующиеся столбцы БД.

```bash
# Найти свойства с одинаковым Code
grep -h '"Code":' source/**/*.mtd | sort | uniq -d
```

#### J16. AttachmentGroup Constraints — синхронизация Task/Assignment/Notice
Если Task имеет именованные Constraints в AttachmentGroups, ВСЕ связанные Assignment/Notice ОБЯЗАНЫ иметь идентичную структуру. Либо у всех `Constraints: []`, либо у всех одинаковые именованные ограничения.

```bash
# Проверить: если Task имеет непустые Constraints, Assignment/Notice тоже должны
```

### J17. System.resx — формат ключей ресурсов (КРИТИЧНО)
Файлы `*System.resx` / `*System.ru.resx` ОБЯЗАНЫ использовать ключи формата `Property_<PropertyName>` для подписей свойств.
Формат `Resource_<GUID>` НЕ разрешается runtime DDS 26.1.

```bash
# Найти System.resx с ключами Resource_<GUID> — это ОШИБКА
grep -rn "Resource_[0-9a-f]\{32\}" --include="*System.resx" --include="*System.ru.resx" source/
```

Правильные форматы ключей в System.resx:
- Свойства: `Property_<PropertyName>` (например `Property_Name`, `Property_TIN`)
- Действия: `Action_<ActionName>`
- Перечисления: `Enum_<EnumName>_<Value>`
- Группы контролов: `ControlGroup_<GUID>`
- Формы: `Form_<GUID>`
- `DisplayName` и `CollectionDisplayName` — БЕЗ префикса `Property_`

#### J18. System.resx парность с обычными .resx
Для каждой сущности должны быть ОБЕ пары:
- `{Entity}.resx` + `{Entity}.ru.resx` (пользовательские ресурсы)
- `{Entity}System.resx` + `{Entity}System.ru.resx` (системные ресурсы: подписи свойств, действий)

```bash
# Проверить наличие System.resx для каждой сущности
find source/ -name "*.mtd" -not -name "Module.mtd" | while read mtd; do
  dir=$(dirname "$mtd")
  entity=$(basename "$mtd" .mtd)
  [ ! -f "$dir/${entity}System.resx" ] && echo "MISSING: $dir/${entity}System.resx"
  [ ! -f "$dir/${entity}System.ru.resx" ] && echo "MISSING: $dir/${entity}System.ru.resx"
done
```

### K. Сверка с эталоном

Сравни структуру с платформенными модулями (внутри контейнеров RX) или используй MCP: `search_metadata` / `extract_entity_schema` для получения эталонных примеров:
- Формат .mtd файлов соответствует реальным примерам?
- Все обязательные поля .mtd заполнены?
- Versions массив корректен?

## Формат вывода

```
=== Валидация пакета: {путь} ===

[OK] Структура файлов (3 модуля, 12 сущностей)
[OK] JSON валидность (15 .mtd файлов)
[OK] GUID уникальность (67 GUID)
[OK] .resx парность (12 пар)
[OK] Namespace соответствие (36 .cs файлов)
[OK] PackageInfo.xml (3 модуля)
[OK] settings/Module.json (3 модуля)
[OK] Запрещённые паттерны (0 нарушений)
[WARN] Кросс-проверка: Property "Amount" без контрола на форме
[OK] Сверка с эталоном

Итого: 9 OK, 1 WARN, 0 FAIL
Пакет готов к импорту в DDS.
```

## Предыдущий этап ← `/security-audit`
## После валидации → `/push-all` (коммит в репозиторий)

## Справочные материалы
- DDS known issues → CLAUDE.md
- Полная валидация → `/validate-all`