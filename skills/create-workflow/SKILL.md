---
description: "Создать полный workflow: блоки в Task.mtd, {TaskName}Scheme.xml, хендлеры блоков и маршрута"
---

> Подробнее о поиске примеров: `docs/platform/REFERENCE_CODE.md` | `dds-examples-map.md`

# Создание Workflow (блоки + схема маршрута + хендлеры) для задачи Directum RX

## ШАГ 0: Посмотри рабочий пример

**Эталон: MapApprovalTask** — задача согласования карты целей из DirRX.Targets.

| Файл | Путь |
|------|------|
| **Task.mtd (Blocks)** | `targets/source/DirRX.Targets/DirRX.Targets.Shared/MapApprovalTask/MapApprovalTask.mtd` |
| **Scheme.xml** | `targets/source/DirRX.Targets/DirRX.Targets.Server/MapApprovalTask/MapApprovalTaskScheme.xml` |
| **BlockHandlers** | `targets/source/DirRX.Targets/DirRX.Targets.Server/MapApprovalTask/MapApprovalTaskBlockHandlers.cs` |
| **RouteHandlers** | `targets/source/DirRX.Targets/DirRX.Targets.Server/MapApprovalTask/MapApprovalTaskRouteHandlers.cs` |
| **TaskHandlers** | `targets/source/DirRX.Targets/DirRX.Targets.Server/MapApprovalTask/MapApprovalTaskHandlers.cs` |

Перед созданием нового workflow — **обязательно прочитай** эти файлы и адаптируй.

## Входные данные
Спроси у пользователя (если не указано):
- **CompanyCode** — код компании (например, `Acme`)
- **ModuleName** — имя модуля
- **TaskName** — имя задачи (должна уже существовать)
- **Blocks** — описание блоков (имя, тип, задание/уведомление, обработчики)

---

## ЧАСТЬ 1: Блоки в Task.mtd

### Типы блоков

| Тип | `$type` в MTD | Handler-события |
|-----|--------------|-----------------|
| Assignment | `Sungero.Metadata.AssignmentBlockMetadata, Sungero.Workflow.Shared` | StartAssignment, CompleteAssignment, End |
| Notice | `Sungero.Metadata.NoticeBlockMetadata, Sungero.Workflow.Shared` | StartNotice |
| Script | `Sungero.Metadata.ScriptBlockMetadata, Sungero.Workflow.Shared` | Execute |
| Task | `Sungero.Metadata.TaskBlockMetadata, Sungero.Workflow.Shared` | StartTask |

### Шаблон блоков в Task.mtd

```json
{
  "BlockIds": [],
  "Blocks": [
    {
      "$type": "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<НОВЫЙ-GUID>",
      "Name": "ApprovalBlock",
      "EntityType": "<GUID-типа-задания>",
      "HandledEvents": [
        "ApprovalBlockStartAssignment",
        "ApprovalBlockCompleteAssignment",
        "ApprovalBlockEnd"
      ],
      "ProcessStagesDisplayMode": "Show",
      "Properties": [
        {
          "$type": "Sungero.Metadata.BooleanBlockPropertyMetadata, Sungero.Metadata",
          "NameGuid": "<GUID>",
          "Name": "NeedRework"
        }
      ]
    },
    {
      "$type": "Sungero.Metadata.ScriptBlockMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<НОВЫЙ-GUID>",
      "Name": "UpdateStatusBlock",
      "HandledEvents": [
        "UpdateStatusBlockExecute"
      ],
      "ProcessStagesDisplayMode": "Hide"
    },
    {
      "$type": "Sungero.Metadata.NoticeBlockMetadata, Sungero.Workflow.Shared",
      "NameGuid": "<НОВЫЙ-GUID>",
      "Name": "NotifyBlock",
      "EntityType": "<GUID-типа-уведомления>",
      "HandledEvents": [
        "NotifyBlockStartNotice"
      ]
    }
  ]
}
```

**КРИТИЧНО:**
- `BlockIds: []` — ВСЕГДА пустой массив (НЕ GUID-ы!)
- `HandledEvents` — точные имена методов: `{BlockName}{Event}`
- `EntityType` — GUID типа задания/уведомления (для Assignment и Notice блоков)
- `Name` блока = префикс всех handler-методов

### Scheme в Task.mtd

```json
{
  "Scheme": {
    "NameGuid": "c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1",
    "Name": "RouteScheme",
    "IsAncestorMetadata": true,
    "VersionsCounter": 1
  },
  "UseSchemeFromSettings": true
}
```

**Scheme.NameGuid** — ФИКСИРОВАННЫЙ: `c7ae4ee8-f2a6-4784-8e61-7f7f642dbcd1` (для всех задач).

---

## ЧАСТЬ 2: {TaskName}Scheme.xml — граф маршрута

Файл размещается в `...Server/{TaskName}/{TaskName}Scheme.xml`.

### Типы блоков в XML

| xsi:type | Описание | Обязательные элементы |
|----------|----------|----------------------|
| `StartBlock` | Начало маршрута (один на схему) | `<Id>` |
| `FinishBlock` | Конец маршрута (один на схему) | `<Id>`, `<UnderReview>` |
| `AssignmentBlock` | Блок задания — создаёт Assignment | `<Id>`, `<BlockGuid>` (= NameGuid из MTD) |
| `ScriptBlock` | Блок скрипта — выполняет код | `<Id>`, `<BlockGuid>` |
| `NoticeBlock` | Блок уведомления — создаёт Notice | `<Id>`, `<BlockGuid>` |

### Минимальная схема (Start → Finish)

```xml
<?xml version="1.0" encoding="utf-8"?>
<RouteScheme xmlns:xsd="http://www.w3.org/2001/XMLSchema"
             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Blocks>
    <BlockBase xsi:type="StartBlock">
      <Id>1</Id>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
    </BlockBase>
    <BlockBase xsi:type="FinishBlock">
      <Id>2</Id>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
      <UnderReview>false</UnderReview>
      <ReviewAssignmentId>0</ReviewAssignmentId>
    </BlockBase>
  </Blocks>
  <Edges>
    <Edge>
      <Id>1</Id>
      <Source>1</Source>
      <Target>2</Target>
    </Edge>
  </Edges>
</RouteScheme>
```

### Полная схема с Assignment, Script и Notice блоками

```
Граф маршрута:

  [Start:1] → [AssignmentBlock:3] → [ScriptBlock:4] → [NoticeBlock:5] → [Finish:2]
```

```xml
<?xml version="1.0" encoding="utf-8"?>
<RouteScheme xmlns:xsd="http://www.w3.org/2001/XMLSchema"
             xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
             MaxBlockId="5">
  <Blocks>
    <!-- Старт маршрута (всегда Id=1) -->
    <BlockBase xsi:type="StartBlock">
      <Id>1</Id>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
    </BlockBase>

    <!-- Финиш маршрута (всегда Id=2) -->
    <BlockBase xsi:type="FinishBlock">
      <Id>2</Id>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
      <UnderReview>false</UnderReview>
      <ReviewAssignmentId>0</ReviewAssignmentId>
    </BlockBase>

    <!-- Блок задания — ссылается на NameGuid блока из Task.mtd -->
    <BlockBase xsi:type="AssignmentBlock">
      <Id>3</Id>
      <BlockGuid>NameGuid-блока-из-Task.mtd</BlockGuid>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
    </BlockBase>

    <!-- Блок скрипта -->
    <BlockBase xsi:type="ScriptBlock">
      <Id>4</Id>
      <BlockGuid>NameGuid-скрипт-блока-из-Task.mtd</BlockGuid>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
    </BlockBase>

    <!-- Блок уведомления -->
    <BlockBase xsi:type="NoticeBlock">
      <Id>5</Id>
      <BlockGuid>NameGuid-notice-блока-из-Task.mtd</BlockGuid>
      <BlockTypeId xsi:nil="true" />
      <ProcessStagesDisplayMode xsi:nil="true" />
      <CustomProperties />
      <ParameterOperations />
      <Operations />
    </BlockBase>
  </Blocks>

  <Edges>
    <!-- Start → AssignmentBlock -->
    <Edge>
      <Id>1</Id>
      <Source>1</Source>
      <Target>3</Target>
    </Edge>
    <!-- AssignmentBlock → ScriptBlock -->
    <Edge>
      <Id>2</Id>
      <Source>3</Source>
      <Target>4</Target>
    </Edge>
    <!-- ScriptBlock → NoticeBlock -->
    <Edge>
      <Id>3</Id>
      <Source>4</Source>
      <Target>5</Target>
    </Edge>
    <!-- NoticeBlock → Finish -->
    <Edge>
      <Id>4</Id>
      <Source>5</Source>
      <Target>2</Target>
    </Edge>
  </Edges>
</RouteScheme>
```

### Условная маршрутизация (ветвление)

Когда из одного блока нужно перейти в разные блоки по условию — создаётся несколько Edge от одного Source. Условие определяется в RouteHandlers.

```
Граф с ветвлением:

  [Start:1] → [ApprovalBlock:3] ──(Approved)──→ [UpdateStatusBlock:4] → [NotifyBlock:5] → [Finish:2]
                                  └─(Rework)───→ [ReworkBlock:6] → [ApprovalBlock:3]
                                  └─(Rejected)──→ [Finish:2]
```

```xml
<Edges>
  <!-- Start → ApprovalBlock -->
  <Edge><Id>1</Id><Source>1</Source><Target>3</Target></Edge>

  <!-- ApprovalBlock → UpdateStatusBlock (Approved) -->
  <Edge><Id>2</Id><Source>3</Source><Target>4</Target></Edge>

  <!-- ApprovalBlock → ReworkBlock (Rework/доработка) -->
  <Edge><Id>3</Id><Source>3</Source><Target>6</Target></Edge>

  <!-- ApprovalBlock → Finish (Rejected) -->
  <Edge><Id>4</Id><Source>3</Source><Target>2</Target></Edge>

  <!-- UpdateStatusBlock → NotifyBlock -->
  <Edge><Id>5</Id><Source>4</Source><Target>5</Target></Edge>

  <!-- NotifyBlock → Finish -->
  <Edge><Id>6</Id><Source>5</Source><Target>2</Target></Edge>

  <!-- ReworkBlock → ApprovalBlock (возврат на согласование) -->
  <Edge><Id>7</Id><Source>6</Source><Target>3</Target></Edge>
</Edges>
```

### Правила Id в Scheme.xml

| Правило | Описание |
|---------|----------|
| `<Id>` блоков | Целые числа, уникальные в пределах схемы. StartBlock = 1, FinishBlock = 2, остальные с 3+ |
| `<Id>` рёбер | Целые числа, уникальные в пределах секции Edges. Нумерация с 1 |
| `MaxBlockId` | Атрибут `<RouteScheme MaxBlockId="N">` — максимальный Id блока в схеме |
| `<BlockGuid>` | Должен совпадать с `NameGuid` блока из `Blocks[]` в Task.mtd |
| `<Source>` / `<Target>` | Ссылаются на `<Id>` блоков (НЕ на GUID) |

---

## ЧАСТЬ 3: Хендлеры блоков

### Namespace и файлы

```
source/{Company}.{Module}/
  ...Server/{TaskName}/
    {TaskName}BlockHandlers.cs    # Хендлеры блоков (namespace: ...Server.{TaskName}Blocks)
    {TaskName}RouteHandlers.cs    # Хендлеры маршрута — условия переходов
    {TaskName}Handlers.cs         # Хендлеры задачи (BeforeStart и др.)
    {TaskName}Scheme.xml          # Схема маршрута
  ...Shared/{TaskName}/
    {TaskName}.mtd                # Metadata (Blocks[], Scheme)
```

### Assignment Block — сигнатуры хендлеров

```csharp
namespace {Company}.{Module}.Server.{TaskName}Blocks
{
  partial class {BlockName}Handlers
  {
    // Вызывается при создании задания — задать тему, срок, исполнителя
    public virtual void {BlockName}StartAssignment(
      {Company}.{Module}.I{AssignmentType} assignment)
    {
      assignment.Subject = _obj.Subject;
      assignment.Deadline = _obj.MaxDeadline;
      assignment.Performer = _obj.Assignee;
    }

    // Вызывается при завершении задания — обработать результат
    public virtual void {BlockName}CompleteAssignment(
      {Company}.{Module}.I{AssignmentType} assignment)
    {
      var result = assignment.Result;
      if (result == {Module}.{AssignmentType}.Result.Approve)
      {
        // бизнес-логика при согласовании
      }
    }

    // Вызывается при завершении блока — доступны ВСЕ созданные задания
    public virtual void {BlockName}End(
      System.Collections.Generic.IEnumerable<{Company}.{Module}.I{AssignmentType}> createdAssignments)
    {
      var last = createdAssignments.OrderByDescending(s => s.Created).FirstOrDefault();
      _block.OutProperties.PropName = last?.Result?.Value;
    }
  }
}
```

### Script Block — сигнатура хендлера

```csharp
namespace {Company}.{Module}.Server.{TaskName}Blocks
{
  partial class {BlockName}Handlers
  {
    // Вызывается при выполнении блока скрипта
    public virtual void {BlockName}Execute()
    {
      _block.RetrySettings.Retry = false;

      // Доступ к задаче
      var task = _obj;

      // Доступ к вложениям
      var attachments = _block.Attachments.ToList();

      // Бизнес-логика
      Logger.Debug("{BlockName}Execute: processing task {id}", task.Id);
    }
  }
}
```

### Notice Block — сигнатура хендлера

```csharp
namespace {Company}.{Module}.Server.{TaskName}Blocks
{
  partial class {BlockName}Handlers
  {
    // Вызывается при создании уведомления
    public virtual void {BlockName}StartNotice(
      {Company}.{Module}.I{NoticeType} notice)
    {
      notice.Subject = _obj.Subject;
      notice.Performer = _obj.Author;
    }
  }
}
```

### Task Block — сигнатура хендлера

```csharp
namespace {Company}.{Module}.Server.{TaskName}Blocks
{
  partial class {BlockName}Handlers
  {
    // Вызывается при создании подзадачи
    public virtual void {BlockName}StartTask(
      {Company}.{Module}.I{SubTaskType} task)
    {
      task.Subject = _obj.Subject;
      task.Attachments.Add(_obj.DocumentGroup.OfficialDocuments.First());
    }
  }
}
```

### RouteHandlers — условия переходов между блоками

Условия ветвления определяются через `Result`-методы. Каждый Result-метод привязан к Edge в схеме.

```csharp
namespace {Company}.{Module}.Server
{
  partial class {TaskName}RouteHandlers
  {
    // Условие перехода: ApprovalBlock → UpdateStatusBlock (Edge Id=2)
    // Метод возвращает true, если нужно идти по этому ребру
    public virtual bool ApprovalBlockResult()
    {
      // Проверить результат блока задания
      var lastAssignment = Assignments.GetAll()
        .Where(a => Equals(a.Task, _obj))
        .OrderByDescending(a => a.Created)
        .FirstOrDefault();

      return lastAssignment?.Result == {Module}.{AssignmentType}.Result.Approve;
    }

    // Условие перехода: ApprovalBlock → ReworkBlock (Edge Id=3)
    public virtual bool ApprovalBlockReworkResult()
    {
      var lastAssignment = Assignments.GetAll()
        .Where(a => Equals(a.Task, _obj))
        .OrderByDescending(a => a.Created)
        .FirstOrDefault();

      return lastAssignment?.Result == {Module}.{AssignmentType}.Result.ForRevision;
    }
  }
}
```

**ВАЖНО:** Для ветвления нужно добавить Result-хендлеры в `HandledEvents` блока в Task.mtd:
```json
{
  "Name": "ApprovalBlock",
  "HandledEvents": [
    "ApprovalBlockStartAssignment",
    "ApprovalBlockCompleteAssignment",
    "ApprovalBlockResult",
    "ApprovalBlockReworkResult"
  ]
}
```

### TaskHandlers — события задачи

```csharp
namespace {Company}.{Module}
{
  partial class {TaskName}ServerHandlers
  {
    // Проверки перед стартом маршрута
    public override void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)
    {
      base.BeforeStart(e);

      if (!_obj.Attachments.Any())
        e.AddError("Необходимо прикрепить документ");
    }
  }
}
```

---

## Доступные контексты в хендлерах

| Контекст | Где доступен | Описание |
|----------|-------------|----------|
| `_obj` | Все хендлеры | Родительская задача |
| `_block` | BlockHandlers | Конфигурация текущего блока |
| `_block.OutProperties.{Prop}` | BlockHandlers | Выходные свойства блока |
| `_block.RetrySettings.Retry` | ScriptBlock | Управление повтором скрипта |
| `_block.Attachments` | BlockHandlers | Вложения блока |
| `_block.Performers` | BlockHandlers | Исполнители блока |
| `_block.Author` | BlockHandlers | Автор блока |
| `_block.{Prop}.GetValueOrDefault()` | BlockHandlers | Nullable свойства блока |
| `_block.SendNotificationAboutAborting` | BlockHandlers | Уведомление при прерывании |

---

## Алгоритм создания workflow

1. **Прочитай Task.mtd** — определи существующие блоки и задания
2. **Сгенерируй GUID** для новых блоков (`uuidgen` или MCP)
3. **Добавь блоки в `Blocks[]`** в Task.mtd с правильными `$type`, `HandledEvents`
4. **Создай `{TaskName}Scheme.xml`** в `...Server/{TaskName}/`:
   - StartBlock (Id=1) и FinishBlock (Id=2) обязательны
   - Каждый блок из MTD получает свой BlockBase с `<BlockGuid>` = `NameGuid` из MTD
   - Edges связывают блоки по `<Source>` → `<Target>` (ссылки на `<Id>`)
5. **Создай `{TaskName}BlockHandlers.cs`** с правильными сигнатурами
6. **Создай `{TaskName}RouteHandlers.cs`** если есть ветвление (Result-методы)
7. **Добавь `HandledEvents`** в MTD для каждого обработчика
8. **Проверь namespace**: блоки — `{Company}.{Module}.Server.{TaskName}Blocks`, маршрут — `{Company}.{Module}.Server`
9. **Валидация** — `/validate-all`

---

## Справка
- Правила DDS-импорта и валидации: см. `CLAUDE.md`
- Паттерны из реальных решений: `knowledge-base/guides/solutions-reference.md`
- .mtd шаблоны: `knowledge-base/guides/23_mtd_reference.md`
- C# паттерны RX: `knowledge-base/guides/25_code_patterns.md`
- После создания артефакта: `/validate-all`
