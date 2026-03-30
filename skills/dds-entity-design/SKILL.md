---
description: "Проектирование сущностей Directum RX: выбор типа, свойства, формы, resx. Используй перед create-databook/create-document/create-task"
---

# DDS Entity Design — Проектирование сущностей

**Используй ПЕРЕД генерацией.** Этот skill помогает принять правильные решения ДО создания сущности, чтобы не переделывать потом.

## Дерево решений: какой тип выбрать?

```
Нужен workflow (задачи/поручения)?
├── ДА → Task + Assignment + Notice
│         └── /create-task
└── НЕТ
    ├── Нужна сложная фильтрация / Actions на коллекции?
    │   └── ДА → EntityMetadata (Target как reference)
    │             └── targets/source/DirRX.Targets/.../Target/Target.mtd (46KB)
    ├── Нужны дочерние коллекции?
    │   ├── ДА → Document (ElectronicDocument)
    │   │         └── /create-document
    │   └── НЕТ
    │       ├── Нужна карточка в DDS UI?
    │       │   ├── ДА → DatabookEntry
    │       │   │         └── /create-databook
    │       │   └── НЕТ
    │       │       ├── Лёгкие данные (логи, сообщения, активности)?
    │       │       │   └── CrmApiV3 + PostgreSQL (НЕ DDS)
    │       │       └── Key-value настройки?
    │       │           └── CrmApiV3 endpoint (НЕ DDS DatabookEntry)
    │       └── Нужна версионность/согласование?
    │           └── Document
    └── Нужна форма с вкладками?
        └── НЕВОЗМОЖНО в DDS 26.1 (FormTabs не поддерживаются в StandaloneFormMetadata)
```

## Reference: Сложная сущность (Target из Targets)

| Файл | Путь |
|------|------|
| **Target.mtd (46KB, эталон)** | `targets/source/DirRX.Targets/DirRX.Targets.Shared/Target/Target.mtd` |
| **Metric.mtd (35KB)** | `targets/source/DirRX.KPI/DirRX.KPI.Shared/Metric/Metric.mtd` |
| **Каталог** | `targets/REFERENCE_CATALOG.md` |

**Паттерны из Target.mtd:**
- **Actions** — 6 штук с ActionArea (CardAndCollection vs Card), IsMultiSelectAction, иконки (Large/Small)
- **FilterPanel** — FilterGroupMetadata + FilterListMetadata для фильтрации по справочникам
- **CreationAreaMetadata** — контроль области создания
- **RemoteControl + FunctionControl + HyperlinkControl** на формах
- **BaseGuid наследование** — TargetHierarchyElementBase → Target

## Правила именования

### Свойства (Code)
- Уникальный в иерархии наследования
- Префикс модуля для избежания конфликтов: `CrmDealName` вместо `Name` (если `Name` уже есть в базовом типе)
- НЕ зарезервированные слова C#

### Enum Values
- НЕ: `New`, `Default`, `Event`, `Base`, `String`, `Class`, `Object`
- ДА: `NewDeal`, `DefaultPriority`, `EventType`, `BaseRate`

### Модули
- Код модуля: `DirRX.{Domain}` (например `DirRX.CRM`, `DirRX.Deals`)
- Имя без точек в сторонних библиотеках: `NewtonsoftJson` (не `Newtonsoft.Json`)

## Обязательные артефакты сущности

| Файл | Обязательный | Содержимое |
|------|-------------|------------|
| `{Entity}.mtd` | ДА | Метаданные: свойства, формы, действия |
| `{Entity}.resx` | ДА | Пользовательские ресурсы (ResourcesKeys) |
| `{Entity}.ru.resx` | ДА | Русская локализация пользовательских ресурсов |
| `{Entity}System.resx` | ДА | Системные ресурсы: Property_*, Action_*, DisplayName |
| `{Entity}System.ru.resx` | ДА | Русские подписи полей, действий, форм |
| `{Entity}Handlers.cs` (Server) | ДА | Серверные обработчики |
| `{Entity}Handlers.cs` (Client) | ДА | Клиентские обработчики |
| `{Entity}ServerFunctions.cs` | Опц. | Серверные функции |
| `{Entity}ClientFunctions.cs` | Опц. | Клиентские функции |

## System.resx — обязательные ключи

```xml
<!-- Для КАЖДОГО свойства -->
<data name="Property_Name"><value>Наименование</value></data>
<data name="Property_Status"><value>Статус</value></data>

<!-- Для КАЖДОГО действия -->
<data name="Action_ShowCard"><value>Открыть карточку</value></data>

<!-- Для КАЖДОГО enum -->
<data name="Enum_Status_Active"><value>Активный</value></data>
<data name="Enum_Status_Closed"><value>Закрыт</value></data>

<!-- Обязательно для сущности -->
<data name="DisplayName"><value>Сделка</value></data>
<data name="CollectionDisplayName"><value>Сделки</value></data>
```

## Частые ошибки
См. /dds-guardrails — 10 антипаттернов Claude при работе с DDS.

## После проектирования

1. Запустить соответствующий create-* skill
2. Запустить `/validate-all` для проверки
3. Убедиться что ВСЕ DisplayName заполнены