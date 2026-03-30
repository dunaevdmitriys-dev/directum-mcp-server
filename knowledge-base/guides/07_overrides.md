# Перекрытие базовой функциональности (Overrides)

> Источник: `webhelp/WebClient/ru-RU/sds_*.htm` + реальные примеры из https://github.com/DirectumCompany/rx-examples

---

## Концепция перекрытий

Sungero построен на механизме **перекрытий** (overrides) — возможности расширить или изменить поведение базовой платформы без правки её исходников.

Перекрыть можно:
- **Серверные методы** (`Server`) — бизнес-логика, вычисления, работа с данными
- **Клиентские операции** (`Client`) — поведение UI, события форм
- **Общие методы** (`Shared`) — валидация, константы, структуры

Перекрытие — это наследование с `override` в классе вашего модуля. Базовый метод вызывается через `base.MethodName(...)`.

---

## Где пишется код перекрытия

В Directum Development Studio (DDS) выбираете сущность или функцию базового решения → раздел **Функции** → находите нужный метод → кнопка **Перекрыть** → открывается файл `.cs` в вашем модуле.

Структура файла перекрытия:

```csharp
// Namespace вашего модуля
namespace MyCompany.MyModule.Server
{
  partial class ModuleFunctions
  {
    // override базового метода
    public override ReturnType MethodName(ParamType param)
    {
      // Можно вызвать базовую реализацию.
      var baseResult = base.MethodName(param);

      // Ваша логика.
      // ...

      return baseResult;
    }
  }
}
```

---

## Реальные примеры из rx-examples

### 1. GetRolePerformers — роли согласования

Переопределяет, кто является исполнителем роли в маршруте согласования.

```csharp
[Remote(IsPure = true), Public]
public List<Sungero.CoreEntities.IRecipient> GetRolePerformers(
    Sungero.Docflow.IApprovalTask task)
{
  var result = new List<Sungero.CoreEntities.IRecipient>();

  // Получить документ из задачи согласования.
  var document = task.DocumentGroup.OfficialDocuments.FirstOrDefault();

  // Привести к типу договора закупки.
  var contract = Trade.Contracts.As(document);

  if (contract != null && _obj.Type == Purchases.PurchaseApprovalRole.Type.Experts)
  {
    // Добавить всех экспертов из коллекции договора.
    foreach (var item in contract.Experts.Where(x => x.Expert != null))
      result.Add(item.Expert);
  }

  return result;
}
```

**Что здесь важно:**
- `[Remote(IsPure = true)]` — метод вызывается с клиента, не меняет данные
- `[Public]` — доступен из других модулей
- `As(document)` — безопасное приведение типа (возвращает null если не совпадает)

---

### 2. GetStageRecipients — получатели этапа согласования

Переопределяет список получателей для конкретного этапа маршрута:

```csharp
[Remote(IsPure = true), Public]
public override List<IRecipient> GetStageRecipients(
    Sungero.Docflow.IApprovalTask task,
    List<IRecipient> additionalApprovers)
{
  // Получить базовый список получателей.
  var recipients = base.GetStageRecipients(task, additionalApprovers);

  // Найти роль типа "Эксперты" в этапе.
  var role = _obj.ApprovalRoles
    .Where(x => x.ApprovalRole.Type == Purchases.PurchaseApprovalRole.Type.Experts)
    .Select(x => Purchases.PurchaseApprovalRole.As(x.ApprovalRole))
    .Where(x => x != null)
    .FirstOrDefault();

  if (role != null)
  {
    // Добавить исполнителей роли к получателям этапа.
    recipients.AddRange(
      Trade.Purchases.PublicFunctions.PurchaseApprovalRole.Remote
        .GetRolePerformers(role, task));
  }

  return recipients;
}
```

**Паттерн:** вызов `base.GetStageRecipients(...)` → дополнение результата → возврат расширенного списка.

---

### 3. CanConvertToPdfInteractively — управление интерактивной конвертацией

Переопределяет условие: когда конвертация в PDF происходит в интерактивном режиме (не в фоне):

```csharp
public override bool CanConvertToPdfInteractively()
{
  // Базовая проверка.
  if (!base.CanConvertToPdfInteractively())
    return false;

  // Для вложений JPG меньше 1 МБ — интерактивно.
  var lastVersion = _obj.LastVersion;
  if (lastVersion != null)
  {
    var extension = lastVersion.AssociatedApplication?.Extension?.ToLower();
    if (extension == "jpg" || extension == "jpeg")
    {
      return lastVersion.Body.Size < 1024 * 1024; // меньше 1 МБ
    }
  }

  return true;
}
```

---

### 4. Добавление информации на форму задания (Showing)

Добавить информационное сообщение при отображении задания согласования:

```csharp
public override void Showing(Sungero.Domain.Client.ModuleClientBaseFunctionEventArgs e)
{
  base.Showing(e);

  // Проверить полномочия сотрудника для подписания.
  var stage = ApprovalStages.As(_obj.Stage);
  if (stage != null && stage.IsCheckAuthority == true)
  {
    var authorityInfo = Purchase.PublicFunctions.GetInfoAboutAuthority(
      Sungero.Company.Employees.Current,
      _obj.DocumentGroup.OfficialDocuments.FirstOrDefault());

    if (!string.IsNullOrEmpty(authorityInfo))
      e.AddInformation(Purchase.Resources.CheckAuthorityFormat(authorityInfo));
  }
}
```

---

### 5. ConvertToPdfWithMarks — PDF с отметками подписи

Добавить отметки согласования на страницы PDF:

```csharp
public override void ConvertToPdfWithMarks()
{
  // Базовая конвертация.
  base.ConvertToPdfWithMarks();

  // Добавить отметку оплаты для входящих счетов.
  var invoice = Docflow.AccountingDocumentBases.As(_obj);
  if (invoice != null && invoice.InternalApprovalState ==
      Docflow.OfficialDocument.InternalApprovalState.Signed)
  {
    UpdatePaymentMark(invoice);
  }
}

private void UpdatePaymentMark(Docflow.IAccountingDocumentBase invoice)
{
  var markHtml = GetPaymentMarkAsHtml(invoice);
  if (!string.IsNullOrEmpty(markHtml))
  {
    // Нанести отметку на первую страницу.
    Docflow.PublicFunctions.OfficialDocument.AddSignatureMarkOnFirstPage(
      _obj, markHtml);
  }
}
```

---

## Атрибуты методов

| Атрибут | Значение |
|---------|----------|
| `[Public]` | Метод доступен из других модулей |
| `[Remote]` | Может вызываться с клиентской стороны |
| `[Remote(IsPure = true)]` | Remote-метод без побочных эффектов (не изменяет данные) |
| `[Obsolete]` | Устаревший метод |

---

## Типичные сценарии перекрытий

### Когда перекрывать серверный метод

- Изменить бизнес-логику (получатели согласования, условия перехода)
- Добавить дополнительные шаги к существующему процессу
- Расширить набор получателей / исполнителей роли
- Изменить логику конвертации или обработки документов

### Когда перекрывать клиентский метод/событие

- Изменить видимость/доступность полей (`IsVisible`, `IsEnabled`, `IsRequired`)
- Добавить информационные сообщения (`AddInformation`, `AddWarning`)
- Контролировать действия пользователя (заблокировать действие)

### Когда НЕ перекрывать, а создавать новое

- Новая самостоятельная функциональность, не связанная с базовой
- Новый тип документа (создать свою сущность, а не перекрывать существующую)
- Новый маршрут согласования (создать новый ProcessKind)

---

## Структура перекрытий в проекте

```
MyModule/
  Server/
    ModuleFunctions.cs          # Перекрытие функций модуля
    ApprovalRoleFunctions.cs    # Перекрытие конкретной роли
  Client/
    DocumentHandlers.cs         # Перекрытие событий документа
  Shared/
    SharedFunctions.cs          # Перекрытие общих функций
```

---

## Публичные функции (PublicFunctions)

Для вызова методов между модулями используется паттерн `PublicFunctions`:

```csharp
// Вызов Remote-метода другого модуля с клиента или сервера.
var performers = Trade.Purchases.PublicFunctions.PurchaseApprovalRole.Remote
  .GetRolePerformers(role, task);

// Вызов локального метода того же модуля.
var info = MyModule.PublicFunctions.Module.GetInfoAboutAuthority(employee, document);
```

---

*Источники: sds_perekrytie.htm · rx-examples github.com/DirectumCompany/rx-examples*
