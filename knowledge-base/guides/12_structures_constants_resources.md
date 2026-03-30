# Структуры, константы и ресурсы

> Источник: `sds_struktury.htm`, `sds_konstanty.htm`, `Sungero.Localization.xml`, `CommonLibrary.xml`

---

## Структуры (Structures)

Структуры — объекты с логически связанными полями. Используются для передачи группированных данных между функциями.

**Важно:** использование `Tuple` запрещено платформой. Вместо них — структуры.

### Допустимые типы полей

- Простые: `string`, `int?`, `long?`, `double?`, `decimal?`, `bool?`, `DateTime?`, `Guid?`, `char?`, `short?`, `Uri`
- Перечисления: `Enumeration`, `Enumeration?`
- Ссылки на сущности: `IUser`, `ICompany`, `IEmployee` и т.п.
- Интерфейсы структур и списки интерфейсов
- Списки: `List<int>`, `List<ICompany>` и т.п.

### Создание структуры

В редакторе решения, модуля, типа сущности или отчёта → группа «Структуры и константы» → **Добавить**.

```csharp
/// <summary>
/// Исполнительская дисциплина подразделения.
/// </summary>
partial class DepartmentDiscipline
{
  /// <summary>
  /// Процент дисциплины.
  /// </summary>
  public int? Discipline { get; set; }

  /// <summary>
  /// Подразделение.
  /// </summary>
  public Sungero.Company.IDepartment Department { get; set; }
}
```

### Создание экземпляра

```csharp
// Метод Create() с параметрами (порядок = порядок полей).
var item = Structures.Module.DepartmentDiscipline.Create(85, department);

// Метод Create() без параметров + заполнение полей.
var item = Structures.Module.DepartmentDiscipline.Create();
item.Discipline = 85;
item.Department = department;
```

### Формат вызова Create()

| Где создана структура | Формат |
|----------------------|--------|
| Решение или модуль | `Structures.Module.<Имя>.Create(...)` |
| Перекрытый модуль | `Structures.<Перекрытый модуль>.Module.<Имя>.Create(...)` |
| Тип сущности | `Structures.<Тип>.<Имя>.Create(...)` |
| Перекрытый тип | `Structures.<Модуль>.<Тип>.<Имя>.Create(...)` |
| Отчёт | `Structures.Report.<Имя>.Create(...)` |

### Использование в функции

```csharp
public List<Structures.Module.DepartmentDiscipline> GetDepartmentsDiscipline()
{
  var assignments = Assignments.GetAll()
    .GroupBy(a => a.Performer.Department)
    .ToList();

  return assignments.Select(group =>
    Structures.Module.DepartmentDiscipline.Create(
      this.CalculateDiscipline(group.ToList()),
      group.Key))
    .OrderBy(s => s.Discipline)
    .ToList();
}
```

### Атрибут [Public] — кросс-модульный доступ

Без `[Public]` структура доступна **только** в своём модуле.

С `[Public]` структура доступна из других модулей, но **через интерфейс** (`I<Имя>`):

```csharp
// Модуль Vacations: структура с [Public].
[Public]
partial class Vacation
{
  public DateTime StartVacation { get; set; }
  public DateTime EndVacation { get; set; }
  public IUser Vacationer { get; set; }
}
```

```csharp
// Модуль HRPR: использование структуры из Vacations.
// Обратите внимание: возвращается IVacation (интерфейс), а не Vacation.
public Vacations.Structures.Module.IVacation CreateVacation()
{
  var startDate = Calendar.Today.NextWorkingDay();
  var endDate = startDate.AddWorkingDays(7);
  var vacation = Vacations.Structures.Module.Vacation.Create(
    startDate, endDate, Users.Current);
  return vacation;
}
```

**Правило:** при кросс-модульном обращении к `[Public]`-структуре используйте **интерфейс** (`IVacation`), а не сам класс (`Vacation`).

### Ограничения

- В перекрытиях и наследниках можно **добавлять** новые структуры
- **Нельзя** переопределять структуры из родительского модуля или типа

---

## Константы (Constants)

Константы — неизменяемые значения, доступные в коде. Используются для срока по умолчанию, имени роли, GUID и т.п.

### Допустимые типы

- Простые: `string`, `int`, `long`, `double`, `decimal`, `bool`, `DateTime`, `Guid`, `char`, `short`, `Uri`
- Идентификаторы: `Guid` (объявляется как `static readonly Guid`)

### Создание константы

В редакторе решения, модуля, типа сущности или отчёта → группа «Структуры и константы» → **Добавить**.

```csharp
/// <summary>
/// Имя типа прав "Регистрация".
/// </summary>
public const string RegistrationRightType = "Регистрация";

[Public]
/// <summary>
/// GUID роли "Руководители наших организаций".
/// </summary>
public static readonly Guid BusinessUnitHeadsRole =
  Guid.Parse("03C7A126-83DE-4F8F-908B-3ACB868E30C5");
```

### Группировка в классы

Для логической организации используйте вложенные статические классы:

```csharp
public static class Module
{
  public static class TypeRights
  {
    public const string RegistrationRightType = "Регистрация";
    public const string DeleteRightType = "Удаление";
  }

  public static class Role
  {
    public const string RoleNameClerks = "Делопроизводители";
    public const string RoleNameRegistrationManagers =
      "Ответственные за настройку регистрации";
  }
}
```

Обращение: `Constants.Module.TypeRights.RegistrationRightType`

### Формат обращения к константам

| Где создана | Формат |
|------------|--------|
| Модуль или решение | `Constants.Module.<Имя>` |
| Перекрытый модуль | `Constants.<Перекрытый модуль>.Module.<Имя>` |
| Тип сущности | `Constants.<Тип>.<Имя>` |
| Перекрытый тип | `Constants.<Модуль>.<Тип>.<Имя>` |
| Отчёт | `Constants.Report.<Имя>` |
| Из другого модуля ([Public]) | `<Модуль>.PublicConstants.Module.<Имя>` |
| Тип из другого модуля ([Public]) | `<Модуль>.PublicConstants.<Тип>.<Имя>` |

### Пример использования

```csharp
// Константа в модуле.
public const int DocumentReviewDefaultDays = 3;

// Использование в серверной функции.
[Remote]
public virtual int GetDocumentReviewDefaultDays()
{
  return Constants.Module.DocumentReviewDefaultDays;
}

// Использование с рабочим календарём.
var deadline = Calendar.Today.AddWorkingDays(
  employee,
  Constants.Module.DocumentReviewDefaultDays);
```

### Ограничения

- Константы **нельзя** переопределять или наследовать
- Без `[Public]` доступны только в рамках своего модуля
- С `[Public]` доступны через `PublicConstants`

---

## Ресурсы (Resources)

Ресурсы — локализованные строки для сообщений, заголовков, подписей.

### Создание ресурсов

В DDS: редактор модуля или типа сущности → узел **«Ресурсы»** → **Добавить**. Задайте:
- **Имя** (ключ, латиницей)
- **Значение** для каждого языка (ru-RU, en-US)

### Использование

```csharp
// Простая строка.
var message = MyModule.Resources.DocumentProcessed;
Logger.Debug(message.ToString());

// Строка с параметрами (Format).
var message = MyModule.Resources.DocumentProcessedFormat(document.Name, Calendar.Today);
e.AddInformation(message);

// В диалогах.
var dialog = Dialogs.CreateInputDialog(MyModule.Resources.SearchTitle);
```

### Ресурсы типа сущности

```csharp
// Ресурсы текущего типа сущности.
var msg = MyEntity.Resources.CannotDelete;
e.AddError(msg);

// С параметрами.
var msg = MyEntity.Resources.AmountExceededFormat(_obj.Amount, maxAmount);
```

### Тип LocalizedString

Ресурсы возвращают `LocalizedString` — многоязычную строку.

| Метод | Описание |
|-------|----------|
| `Append(LocalizedString)` | Объединить с другой строкой |
| `AppendFormat(format, args)` | Объединить с форматированной строкой |
| `ToString()` | Получить строку в текущей локали |

```csharp
var result = MyModule.Resources.Header;
result = result.Append(MyModule.Resources.Separator);
result = result.AppendFormat(" ({0})", document.Name);
Logger.Debug(result.ToString());
```

---

## Перечисления (Enumeration)

Перечисления задаются в DDS как свойства типа сущности с фиксированным набором значений.

### Использование

```csharp
// Проверка значения перечисления.
if (_obj.Status == MyEntity.Status.Active)
  Logger.Debug("Запись активна.");

// Установка значения.
_obj.Priority = MyEntity.Priority.High;

// В LINQ-запросах.
var active = MyEntities.GetAll(e => e.Status == MyEntity.Status.Active);
```

### Создание нового значения перечисления программно

```csharp
// Через конструктор (для кастомных значений, если это поддерживается типом).
var customStatus = new Enumeration("CustomStatus");
```

---

*Источники: sds_struktury.htm · sds_konstanty.htm · Sungero.Localization.xml · CommonLibrary.xml*
