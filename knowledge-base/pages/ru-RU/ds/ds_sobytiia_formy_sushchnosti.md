---
id: ds_sobytiia_formy_sushchnosti
module: ds
role: Developer
topic: Клиентские события сущности
breadcrumb: "Разработка > Программный код > События типов сущностей"
description: "Какие есть, существуют события формы сущности"
source: webhelp/WebClient/ru-RU/ds_sobytiia_formy_sushchnosti.htm
---

# Клиентские события сущности

События формы сущности и другие задаются в редакторе типа сущности в группе «Клиентские события». Выполняются в клиентском приложении.

Клиентские события
События, которые есть у всех типов сущностей
Показ формы | Showing
Закрытие формы | Closing
Обновление формы | Refresh
События только для типов документов
До показа диалога подписания | ShowingSignDialog

## Показ формы (Showing)

```csharp
public override void Showing(Sungero.Presentation.FormShowingEventArgs e)
{
// Не показывать свойство Department, если карточка справочника "Сотрудники"
// была открыта из карточки справочника "Подразделения".
base.Showing(e);
if (CallContext.CalledFrom(Departments.Info))
_obj.State.Properties.Department.IsVisible = false;
}
```

## Закрытие формы (Closing)

```csharp
// При закрытии карточки входящего счета сделать необязательными для заполнения
// свойства Номер счета, Дата счета, Сумма и Валюта.
public override void Closing(Sungero.Presentation.FormClosingEventArgs e)
{
base.Closing(e);

_obj.State.Properties.Number.IsRequired = false;
_obj.State.Properties.Date.IsRequired = false;
_obj.State.Properties.TotalAmount.IsRequired = false;
_obj.State.Properties.Currency.IsRequired = false;
}
```

## Обновление формы (Refresh)

```csharp
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
// Изменить заголовок окна карточки в соответствии с именем документа.
e.Title = _obj.Name == Docflow.Resources.DocumentNameAutotext ? null : _obj.Name;
}
```

## До показа диалога подписания (ShowingSignDialog)

```csharp
// До показа диалога подписания проверить ошибки валидации.
public override void ShowingSignDialog(Sungero.Domain.Client.ShowingSignDialogEventArgs e)
{
var errors = Functions.OfficialDocument.Remote.GetApprovalValidationErrors(_obj, true);
foreach (var error in errors)
// Отобразить подсказку, если есть ошибки.
e.Hint.Add(error);
// Если нет ошибок, будет разрешена утверждающая подпись.
e.CanApprove = !errors.Any();
}
```

**См. также**

Пример. Передача данных между событиями одной сущности Валидация
