# StateView — визуальное отображение состояния

> Источник: `om_stateview.htm`, `om_stateview_stateblock.htm`, `om_stateview_addblock.htm`, `om_stateview_adddefaultlabel.htm`, `om_stateview_adddefaulthyperlink.htm`, `om_stateview_getblockcount.htm`, `om_stateviewlabel.htm`, `om_stateblockhyperlinkstyle.htm`, `om_stateblock_addhyperlink.htm`, `om_getprocessstagesstateview.htm`

---

## Обзор

Контрол состояния (StateView) — визуальный элемент карточки для отображения этапов обработки сущности. Например, на вкладке «Задачи» документа показывается история процесса согласования.

### Иерархия классов

| Класс | Описание |
|-------|----------|
| `StateView` | Модель контрола состояния |
| `StateBlock` | Блок контрола |
| `StateBlockContent` | Контейнер содержимого блока |
| `StateViewLabel` | Текстовая метка по умолчанию |
| `StateBlockLabelStyle` | Стиль текста в блоке |
| `StateBlockHyperlinkStyle` | Стиль гиперссылки в блоке |

### Перечисления

| Перечисление | Описание |
|-------------|----------|
| `StateBlockIconType` | Тип иконки: User, OftenUsed, Comment и др. |
| `StateBlockIconSize` | Размер: Small, Large |
| `FontWeight` | Толщина шрифта |
| `DockType` | Тип прикрепления блока |

---

## StateView — модель

### Создание

```csharp
var stateView = StateView.Create();
```

### Методы

| Метод | Описание |
|-------|----------|
| `AddBlock()` | Добавить пустой блок |
| `AddBlock(StateBlock)` | Добавить копию существующего блока |
| `AddDefaultLabel(text)` | Текст, отображаемый когда нет блоков |
| `AddDefaultHyperlink(text, uri)` | Гиперссылка, отображаемая когда нет блоков |
| `GetBlockCount()` | Общее число блоков (включая дочерние) |

### Свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Blocks` | `IEnumerable<StateBlock>` | Коллекция блоков |
| `DefaultLabels` | `IEnumerable<StateViewLabel>` | Метки по умолчанию |
| `MaxElementCount` | `int` | Макс. элементов (настраивается через `STATEVIEW_MAX_ELEMENT_COUNT`) |
| `IsPrintable` | `bool` | Доступен ли для печати |

---

## StateBlock — блок

### Основные операции

```csharp
// Создать блок.
var block = stateView.AddBlock();

// Назначить иконку.
block.AssignIcon(StateBlockIconType.User, StateBlockIconSize.Small);

// Добавить текст.
block.AddLabel("Текст действия пользователя.");

// Перенос строки.
block.AddLineBreak();

// Привязать к сущности (клик открывает карточку).
block.Entity = assignment;
```

### Добавление гиперссылки

```csharp
// Без стиля.
block.AddHyperlink("Правило согласования", Hyperlinks.Get(approvalRule));

// Со стилем.
var style = StateBlockHyperlinkStyle.Create();
style.FontSize = 14;
block.AddHyperlink("Правило согласования", Hyperlinks.Get(approvalRule), style);
```

### Копирование блока

```csharp
// Добавить блок с иконкой и текстом.
var block = stateView.AddBlock();
block.AssignIcon(StateBlockIconType.User, StateBlockIconSize.Small);
block.AddLabel("Действие пользователя.");

// Добавить копию блока (с теми же иконкой и текстом).
stateView.AddBlock(block);
```

---

## Текст и гиперссылки по умолчанию

Отображаются когда в контроле нет блоков:

```csharp
// Текст по умолчанию.
stateView.AddDefaultLabel("Нет задач, в которые вложен документ");

// Гиперссылка по умолчанию.
var sender = _obj.Counterparty;
stateView.AddDefaultHyperlink(sender.Name, Hyperlinks.Get(sender));
```

### Настройка стиля метки

```csharp
stateView.AddDefaultLabel("Текст по умолчанию");
var label = stateView.DefaultLabels.First();
label.Style.FontSize = 16;
```

---

## Полный пример: блок задачи на согласование

```csharp
public StateView GetApprovalStateView()
{
  var stateView = StateView.Create();

  // Блок задачи.
  var taskBlock = stateView.AddBlock();
  taskBlock.Entity = _obj;

  // Иконка 32x32.
  taskBlock.AssignIcon(
      OfficialDocuments.Info.Actions.SendForApproval,
      StateBlockIconSize.Large);

  // Заголовок блока.
  var headerStyle = StateBlockLabelStyle.Create();
  headerStyle.FontWeight = FontWeight.Bold;
  taskBlock.AddLabel(ApprovalTasks.Resources.Approval, headerStyle);

  // Перенос строки.
  taskBlock.AddLineBreak();

  // Гиперссылка на правило согласования.
  taskBlock.AddHyperlink(
      _obj.ApprovalRule.Name,
      Hyperlinks.Get(_obj.ApprovalRule));

  // Блок исполнителя.
  var performerBlock = stateView.AddBlock();
  performerBlock.AssignIcon(StateBlockIconType.User, StateBlockIconSize.Small);
  performerBlock.AddLabel(GetUserActionText(user, action, substituted) + ".");

  return stateView;
}
```

---

## GetProcessStagesStateView — этапы процесса задачи

Доступен для объектов Task. Получает модель контрола состояния этапов процесса.

```csharp
// Получить модель состояния этапов процесса.
var task = Workflow.Tasks.GetAll().First();
var stageView = task.GetProcessStagesStateView();
```

---

## StateBlockContent — контейнер

Контейнер для размещения содержимого внутри блока. Поддерживает гиперссылки:

```csharp
// С Uri-объектом.
content.AddHyperlink("Текст ссылки", new Uri("https://example.com"));

// Со стилем.
var style = StateBlockHyperlinkStyle.Create();
style.FontSize = 12;
content.AddHyperlink("Текст", "https://example.com", style);
```

---

## Добавление контрола состояния на форму

1. В DDS добавить элемент «Контрол состояния» на форму сущности
2. На панели свойств через «Добавить функцию» — создать серверную функцию
3. Серверная функция строит модель `StateView` и возвращает её

---

*Источники: om_stateview.htm · om_stateview_stateblock.htm · om_stateview_addblock.htm · om_stateview_adddefaultlabel.htm · om_stateview_adddefaulthyperlink.htm · om_stateview_getblockcount.htm · om_stateviewlabel.htm · om_stateblockhyperlinkstyle.htm · om_stateblock_addhyperlink.htm · om_stateblockcontent_addhyperlink.htm · om_getprocessstagesstateview.htm*
