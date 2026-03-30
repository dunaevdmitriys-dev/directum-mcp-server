# Диалоги и пользовательский интерфейс

> Источник: `om_dialogi.htm`, `om_dialog_s_zaprosom_parametrov_createinputdialog.htm`, `om_soobshchenie_ob_oshibke_showmessage.htm`, `Sungero.Dialogs.xml`

---

## Обзор

Класс `Dialogs` — точка входа для всех диалогов. **Доступен только в клиентском коде.**

| Метод | Тип диалога |
|-------|------------|
| `Dialogs.ShowMessage(...)` | Сообщение (ошибка, предупреждение, информация) |
| `Dialogs.NotifyMessage(...)` | Всплывающее уведомление (автоисчезающее) |
| `Dialogs.CreateConfirmDialog(...)` | Подтверждение (Да/Нет) |
| `Dialogs.CreateTaskDialog(...)` | Выбор варианта действия |
| `Dialogs.CreateInputDialog(...)` | Ввод параметров с контролами |
| `Dialogs.CreateSearchDialog<T>(...)` | Поиск сущности по критериям |
| `Dialogs.CreateSelectTypeDialog(...)` | Выбор типа сущности |
| `Dialogs.CreateFileSelectionDialog(...)` | Выбор файла |

---

## ShowMessage — простое сообщение

```csharp
// Информационное сообщение.
Dialogs.ShowMessage("Документ успешно обработан.");

// С типом сообщения.
Dialogs.ShowMessage("Не удалось найти контрагента.", MessageType.Warning);

// С описанием.
Dialogs.ShowMessage("Ошибка обработки", "Документ не найден в системе.", MessageType.Error);

// С заголовком окна.
Dialogs.ShowMessage("Внимание", "Срок договора истекает через 3 дня.",
  MessageType.Warning, "Уведомление");
```

Типы сообщений (`MessageType`): `Information`, `Warning`, `Error`.

---

## NotifyMessage — всплывающее уведомление

Автоматически исчезает, не требует действия пользователя.

```csharp
Dialogs.NotifyMessage("Документ сохранён.");
```

Ограничения: нельзя вставлять переносы строк и ссылки.

---

## CreateConfirmDialog — подтверждение

```csharp
// Простое подтверждение.
var confirm = Dialogs.CreateConfirmDialog("Удалить документ?");
if (confirm.Show())
{
  // Пользователь нажал «Да».
  Functions.Module.Remote.DeleteDocument(_obj.Id);
}

// С описанием.
var confirm = Dialogs.CreateConfirmDialog(
  "Отправить на согласование?",
  "Документ будет направлен руководителю подразделения.");
if (confirm.Show())
  Functions.Module.Remote.SendForApproval(_obj);

// С «Больше не спрашивать».
var confirm = Dialogs.CreateConfirmDialog("Закрыть без сохранения?");
confirm.WithDontAskAgain("MyModule_CloseWithoutSave");
if (confirm.Show())
  e.CloseFormAfterAction = true;
```

---

## CreateTaskDialog — выбор варианта

Диалог с кнопками-вариантами для выбора действия.

```csharp
// Диалог с вариантами.
var dialog = Dialogs.CreateTaskDialog("Выберите действие с документом");
var approve = dialog.Buttons.AddCustom("Согласовать");
var reject = dialog.Buttons.AddCustom("Отклонить");
dialog.Buttons.AddCancel();
dialog.Buttons.Default = approve;

var result = dialog.Show();
if (result == approve)
  Functions.Module.Remote.ApproveDocument(_obj);
else if (result == reject)
  Functions.Module.Remote.RejectDocument(_obj);
```

---

## CreateInputDialog — ввод параметров

Самый мощный диалог. Позволяет добавлять произвольные контролы.

```csharp
var dialog = Dialogs.CreateInputDialog("Параметры отчёта");

// Добавить контролы.
var dateFrom = dialog.AddDate("Дата с", true);
var dateTo = dialog.AddDate("Дата по", true);
var department = dialog.AddSelect("Подразделение", false,
  Sungero.Company.Departments.Null);
var includeArchive = dialog.AddBoolean("Включать архивные", false);

// Показать и обработать.
if (dialog.Show() == DialogButtons.Ok)
{
  var report = Functions.Module.Remote.GenerateReport(
    dateFrom.Value, dateTo.Value,
    department.Value, includeArchive.Value ?? false);
  if (report != null)
    report.ShowCard();
}
```

### Доступные контролы

| Метод | Возвращает | Описание |
|-------|-----------|----------|
| `AddString(title, required)` | `IStringDialogValue` | Строка |
| `AddString(title, required, default)` | `IStringDialogValue` | Строка со значением |
| `AddMultilineString(title, required)` | `IMultilineStringDialogValue` | Многострочный текст |
| `AddPasswordString(title, required)` | `IPasswordStringDialogValue` | Пароль (скрытый) |
| `AddInteger(title, required)` | `IIntegerDialogValue` | Целое число |
| `AddDouble(title, required)` | `IDoubleDialogValue` | Вещественное число |
| `AddDate(title, required)` | `IDateDialogValue` | Дата и время |
| `AddBoolean(title)` | `IBooleanDialogValue` | Флажок |
| `AddBoolean(title, default)` | `IBooleanDialogValue` | Флажок со значением |
| `AddSelect(title, required)` | `IDropDownDialogValue` | Выпадающий список |
| `AddSelect<T>(title, required, default)` | `INavigationDialogValue<T>` | Выбор сущности |
| `AddSelectMany<T>(title, required)` | — | Множественный выбор |
| `AddFileSelect(title, required)` | `IFileSelectDialogValue` | Выбор файла |
| `AddHyperlink(title)` | `IHyperlinkDialogValue` | Гиперссылка |
| `AddProgressBar(title)` | `IProgressBarDialogValue` | Индикатор прогресса |
| `AddIdentifier(title, required)` | `IIdentifierDialogValue` | Идентификатор |

### Формат даты

```csharp
// Только дата (без времени).
var date = dialog.AddDate("Дата", true).AsDateTime();

// Месяц и год.
var monthYear = dialog.AddDate("Период", true).AsMonthYear();

// Только год.
var year = dialog.AddDate("Год", true).AsYear();
```

### Свойства контролов

Каждый контрол поддерживает:

| Свойство/Метод | Описание |
|---------------|----------|
| `.Value` | Текущее значение |
| `.IsEnabled` | Доступность для редактирования |
| `.IsVisible` | Видимость на форме |
| `.IsRequired` | Обязательность |
| `.IsLabelVisible` | Видимость подписи |
| `.WithLabel(text)` | Текстовая метка перед контролом |
| `.WithPlaceholder(text)` | Подсказка в пустом поле |
| `.MaxLength(n)` | Максимальная длина (для строк) |

### Выпадающий список с фиксированными значениями

```csharp
// Список строк.
var priority = dialog.AddSelect("Приоритет", true);
priority.From("Высокий", "Средний", "Низкий");

// Список сущностей (выбор из выпадающего списка конкретных записей).
var user = dialog.AddSelect("Ответственный", true,
  Sungero.CoreEntities.Users.Null);
user.From(user1, user2, user3);

// Выбор сущности из полного справочника.
var employee = dialog.AddSelect("Сотрудник", true,
  Sungero.Company.Employees.Null);
```

### Кнопки диалога

```csharp
// Стандартные кнопки.
dialog.Buttons.AddOkCancel();
dialog.Buttons.AddYesNo();

// Кастомные кнопки.
var save = dialog.Buttons.AddCustom("Сохранить");
var cancel = dialog.Buttons.AddCancel();
dialog.Buttons.Default = save; // Кнопка по Enter

// Проверка результата.
if (dialog.Show() == save)
  DoSomething();
```

Предустановленные кнопки: `AddOk()`, `AddCancel()`, `AddYes()`, `AddNo()`, `AddAbort()`, `AddIgnore()`, `AddRetry()`, `AddOkCancel()`, `AddYesNo()`, `AddYesToAll()`, `AddRetryAbortIgnore()`.

---

## События диалога

### SetOnValueChanged — реакция на изменение

```csharp
var dialog = Dialogs.CreateInputDialog("Расчёт");
var quantity = dialog.AddInteger("Количество", true);
var price = dialog.AddDouble("Цена", true);
var total = dialog.AddDouble("Итого", false);
total.IsEnabled = false; // Только для чтения.

// При изменении количества или цены — пересчитать.
quantity.SetOnValueChanged((args) =>
{
  if (quantity.Value.HasValue && price.Value.HasValue)
    total.Value = quantity.Value * price.Value;
});

price.SetOnValueChanged((args) =>
{
  if (quantity.Value.HasValue && price.Value.HasValue)
    total.Value = quantity.Value * price.Value;
});

dialog.Show();
```

### SetOnRefresh — валидация при обновлении

```csharp
dialog.SetOnRefresh((args) =>
{
  if (dateFrom.Value.HasValue && dateTo.Value.HasValue &&
      dateFrom.Value > dateTo.Value)
    args.AddError("Дата начала не может быть позже даты окончания.");
});
```

### SetOnButtonClick — валидация при нажатии кнопки

```csharp
dialog.SetOnButtonClick((args) =>
{
  if (args.Button == DialogButtons.Ok && string.IsNullOrEmpty(name.Value))
  {
    args.AddError("Заполните имя.");
    args.CloseAfterExecute = false; // Не закрывать диалог.
  }
});
```

### SetOnExecute — обработка клика гиперссылки

```csharp
var link = dialog.AddHyperlink("Открыть справочник");
link.SetOnExecute(() =>
{
  Sungero.Company.Employees.Show();
});
```

---

## CreateSearchDialog — поиск сущности

```csharp
// Поиск документа.
var searchDialog = Dialogs.CreateSearchDialog<IOfficialDocument>("Поиск документа");
if (searchDialog.Show())
{
  var query = searchDialog.GetQuery();
  var documents = query.ToList();
  documents.Show("Результаты поиска");
}
```

---

## CreateSelectTypeDialog — выбор типа

```csharp
// Выбор типа документа для создания.
var typeDialog = Dialogs.CreateSelectTypeDialog("Выберите тип документа");
if (typeDialog.Show())
{
  var selectedType = typeDialog.SelectedType;
  // Создать документ выбранного типа...
}
```

---

## CreateFileSelectionDialog — выбор файла

```csharp
// Выбор одного файла.
var fileDialog = Dialogs.CreateFileSelectionDialog("Выберите файл");
if (fileDialog.Show())
{
  var filePath = fileDialog.SelectedFilePath;
  // Обработать файл...
}
```

---

## Каскадные списки (зависимые контролы)

```csharp
var dialog = Dialogs.CreateInputDialog("Выбор исполнителя");
var department = dialog.AddSelect("Подразделение", true,
  Sungero.Company.Departments.Null);
var employee = dialog.AddSelect("Сотрудник", true,
  Sungero.Company.Employees.Null);

// При смене подразделения — обновить список сотрудников.
department.SetOnValueChanged((args) =>
{
  employee.Value = Sungero.Company.Employees.Null;
  if (department.Value != null)
  {
    employee.From(
      Sungero.Company.Employees.GetAll(e =>
        Equals(e.Department, department.Value) &&
        e.Status == Sungero.CoreEntities.DatabookEntry.Status.Active)
      .ToArray());
  }
});

if (dialog.Show() == DialogButtons.Ok)
{
  // employee.Value — выбранный сотрудник.
}
```

---

## Управление формой сущности

### Видимость и доступность полей

```csharp
// В событии Showing.
_obj.State.Properties.Field.IsVisible = false;   // Скрыть поле
_obj.State.Properties.Field.IsEnabled = false;   // Только для чтения
_obj.State.Properties.Field.IsRequired = true;   // Обязательное

// Все поля — только для чтения.
foreach (var property in _obj.State.Properties)
  property.IsEnabled = false;

// Запретить удаление строк из коллекции.
_obj.State.Properties.Lines.CanDelete = false;
```

### Подсветка контролов

```csharp
_obj.State.Properties.Amount.HighlightColor =
  Sungero.Core.Colors.Parse("#FF0000"); // Красная подсветка
```

### Отображение карточек и списков

```csharp
// Открыть карточку сущности.
document.ShowCard();

// Открыть список.
Sungero.Contracts.Contracts.Show();

// Открыть список с фильтром.
Sungero.Contracts.Contracts.GetAll(c =>
  c.Status == Sungero.Contracts.Contract.Status.Active)
  .Show("Активные договоры");
```

---

## Типичные паттерны

### Подтверждение перед действием

```csharp
public virtual void DeleteAction(Sungero.Domain.Client.ExecuteActionArgs e)
{
  var confirm = Dialogs.CreateConfirmDialog(
    "Вы уверены?",
    "Документ будет удалён без возможности восстановления.");
  if (!confirm.Show())
    return;

  Functions.Module.Remote.DeleteDocument(_obj.Id);
  e.CloseFormAfterAction = true;
}
```

### Диалог с прогрессом

```csharp
var dialog = Dialogs.CreateInputDialog("Обработка");
var progress = dialog.AddProgressBar("Прогресс");
progress.TotalValue = 100;
progress.Value = 0;

// Показать диалог (прогресс обновляется в SetOnRefresh).
dialog.SetOnRefresh((args) =>
{
  progress.Value = Functions.Module.Remote.GetProcessingProgress();
  if (progress.Value >= progress.TotalValue)
    args.AddInformation("Обработка завершена.");
});

dialog.Show();
```

---

*Источники: om_dialogi.htm · om_dialog_s_zaprosom_parametrov_createinputdialog.htm · om_soobshchenie_ob_oshibke_showmessage.htm · om_vsplvaiushchee_soobshchenie_notifymessage.htm · om_createconfirmdialog.htm · om_knopki_dialoga_buttons.htm · om_stroka_addstring.htm · om_data_adddate.htm · om_flazhok_addboolean.htm · om_vypadaiushchii_spisok_addselect.htm · om_giperssylka_addhyperlink.htm · om_dialog_s_vyborom_varianta_createtaskdialog.htm · om_createsearchdialog.htm · Sungero.Dialogs.xml*
