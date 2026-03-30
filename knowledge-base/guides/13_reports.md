# Разработка отчётов

> Источник: `sds_sobytiia_otcheta.htm`, `sds_poriadok_vypolneniia_otcheta.htm`, `sds_nastroika_maketa_otcheta.htm`, `sds_istochniki_dannykh.htm`, `sds_dobavlenie_parametrov.htm`, `sds_kak_sgenerirovat_otchet.htm`, `Sungero.Reporting.Server.xml`, `Sungero.Reporting.ClientBase.xml`

---

## Обзор

Отчёт — first-class элемент разработки в DDS. Включает:
- **Параметры** — входные данные (даты, сущности, форматы)
- **Источники данных** — откуда берутся данные (сущности или SQL)
- **Макет** — визуальная структура (бэнды, текст, таблицы, диаграммы)
- **События** — логика до и после выполнения (клиент и сервер)

---

## Порядок выполнения отчёта

### Запуск с клиента (Open / ExportToFile)

```
1. Client BeforeExecute  ← запрос параметров у пользователя, валидация
   ↓ e.Cancel = true → отмена генерации
2. Server BeforeExecute  ← вычисление параметров, подготовка данных
3. [Сбор данных из источников]
4. [Рендеринг макета]
5. Server AfterExecute   ← очистка временных данных
6. [Отображение отчёта]
```

### Запуск с сервера (Export / ExportTo)

```
1. Server BeforeExecute  ← подготовка данных
2. [Сбор данных из источников]
3. [Рендеринг макета]
4. Server AfterExecute   ← очистка
5. [Возврат Stream или запись в документ]
```

---

## Параметры отчёта

### Предопределённые параметры

| Параметр | Тип | Описание |
|----------|-----|----------|
| `ExportFormat` | `ReportExportFormat` | Формат вывода (PDF, Word, Excel) |
| `Entity` | Ссылка на сущность | Сущность, для которой строится отчёт. Только для отчётов типа сущности |

### Настройки параметра

| Свойство | Описание |
|----------|----------|
| `Name` | Уникальное имя (латиница) |
| `DataType` | Тип данных (String, DateTime, ссылка на сущность и др.) |
| `IsRequired` | Обязательность (проверяется перед выполнением) |
| `IsCollection` | Коллекция значений (заполняется через `AddRange()`) |
| `Expression` | Выражение для вычисления значения по умолчанию |
| `Description` | Текстовое описание |

### Доступ к параметрам

```csharp
// В обработчиках событий.
OutgoingDocumentsReport.BeginDate
OutgoingDocumentsReport.DocumentRegister

// В выражениях макета (квадратные скобки).
[BeginDate]
[EndDate]
```

---

## События отчёта

### Client BeforeExecute — запрос параметров

Выполняется на клиенте. Используется для запроса параметров через диалоги.

```csharp
public override void BeforeExecute(Sungero.Reporting.Client.BeforeExecuteEventArgs e)
{
  if (!OutgoingDocumentsReport.BeginDate.HasValue &&
      !OutgoingDocumentsReport.EndDate.HasValue)
  {
    var dialog = Dialogs.CreateInputDialog("Журнал исходящих документов");
    var beginDate = dialog.AddDate("Дата начала", true, OutgoingDocumentsReport.BeginDate);
    var endDate = dialog.AddDate("Дата завершения", true, OutgoingDocumentsReport.EndDate);
    var register = dialog.AddSelect("Журнал", true, OutgoingDocumentsReport.DocumentRegister);

    if (dialog.Show() == DialogButtons.Ok)
    {
      OutgoingDocumentsReport.BeginDate = beginDate.Value.Value;
      OutgoingDocumentsReport.EndDate = endDate.Value.Value;
      OutgoingDocumentsReport.DocumentRegister = register.Value;
    }
    else
      e.Cancel = true; // Отмена генерации.
  }
}
```

**Аргументы:**
- `e.Cancel` — `true` → остановить генерацию отчёта.

### Server BeforeExecute — подготовка данных

Выполняется на веб-сервере. Вычисление параметров, подготовка временных таблиц.

```csharp
public override void BeforeExecute(Sungero.Reporting.Server.BeforeExecuteEventArgs e)
{
  // Подготовить временную таблицу с данными.
  Functions.Module.PrepareReportData(
    OutgoingDocumentsReport.BeginDate,
    OutgoingDocumentsReport.EndDate);
}
```

### Server AfterExecute — очистка

Выполняется на веб-сервере после генерации. Очистка временных данных.

```csharp
public override void AfterExecute(Sungero.Reporting.Server.AfterExecuteEventArgs e)
{
  // Удалить временную таблицу.
  if (e.IsSuccessful)
    Functions.Module.CleanupReportData();
}
```

**Аргументы:**
- `e.IsSuccessful` — `true` если генерация прошла успешно.

---

## Источники данных

### Тип 1: На основании типа сущности

Возвращает `IQueryable<T>`. Подходит для простых отчётов с одним типом сущности.

```csharp
// Обработчик источника данных (автогенерируемый).
public virtual IQueryable<Sungero.RecordManagement.IOutgoingLetter> GetOutgoingLetters()
{
  return Sungero.RecordManagement.OutgoingLetters.GetAll();
}

// С фильтрацией по параметрам отчёта.
public virtual IQueryable<Sungero.RecordManagement.IOutgoingLetter> GetOutgoingLetters()
{
  return Sungero.RecordManagement.OutgoingLetters.GetAll()
    .Where(l => l.RegistrationState == Sungero.Docflow.OfficialDocument.RegistrationState.Registered)
    .Where(l => Equals(l.DocumentRegister, OutgoingDocumentsReport.DocumentRegister))
    .Where(l => l.RegistrationDate >= OutgoingDocumentsReport.BeginDate)
    .Where(l => l.RegistrationDate <= OutgoingDocumentsReport.EndDate)
    .OrderBy(l => l.RegistrationNumber)
    .ThenBy(l => l.RegistrationDate);
}
```

### Тип 2: SQL-источник данных

Для сложных отчётов с данными из нескольких таблиц.

**Создание:**
1. Добавить SQL-запрос на сервер
2. В дизайнере: правая кнопка на `Sungero_Connection` → «Новый источник данных»
3. Выбрать SQL-запрос
4. Определить параметры запроса и возвращаемые поля

**Свойства параметров SQL-источника:**

| Свойство | Описание |
|----------|----------|
| `Name` | Имя параметра в SQL-запросе |
| `DataType` | Тип данных |
| `DefaultValue` | Значение по умолчанию |
| `Expression` | Вычисляемое выражение |
| `Size` | Размер (для строк) |

**Свойства полей SQL-источника:**

| Свойство | Описание |
|----------|----------|
| `Name` | Имя поля |
| `Alias` | Псевдоним |
| `DataType` | Тип данных |
| `BindableControl` | Тип объекта: Text, RichText, Picture, CheckBox, Custom |
| `Calculated` | Вычисляемое поле |
| `Expression` | Выражение вычисления |

### Связи между источниками

Устанавливают отношение «главный — подчинённый» (master-detail).

Обращение к полям главного источника из подчинённого:
```
[ChildSource.MasterSource.FieldName]
// Например:
[RegistrationSettings.OurCompanies.Name]
```

---

## Макет отчёта

### Бэнды (структурные контейнеры)

| Бэнд | Когда отображается | Типичное содержимое |
|-------|--------------------|---------------------|
| Заголовок отчёта | Один раз, в начале | Название, дата генерации |
| Подвал отчёта | Один раз, после последних данных | Место для подписи |
| Заголовок страницы | Вверху каждой страницы | Заголовки столбцов таблицы |
| Подвал страницы | Внизу каждой страницы | Номера страниц |
| Данные | По одному на каждую строку источника | Значения полей |
| Заголовок данных | Перед первой строкой данных | Подзаголовок |
| Подвал данных | После последней строки данных | Итоги |
| Заголовок группы | В начале каждой группы | Название группы |
| Подвал группы | В конце каждой группы | Итоги группы |
| Дочерний | Сразу после родительского бэнда | Дополнительная информация |
| Фоновый | На каждой странице как фон | Водяной знак, фоновое изображение |

### Свойства бэндов

| Свойство | Описание |
|----------|----------|
| `Height` | Высота (см) |
| `CanBreak` | Разбивка содержимого между страницами |
| `CanGrow` | Автоувеличение высоты |
| `CanShrink` | Автоуменьшение высоты |
| `KeepChild` | Дочерний бэнд не отрывается от родителя |
| `Printable` | Отображать / скрывать |
| `PrintOn` | На каких страницах: FirstPage, LastPage, OddPages, EvenPages |
| `RepeatOnEveryPage` | Повторять на каждой странице |
| `StartNewPage` | Начинать с новой страницы |

### Подключение бэнда к источнику данных

1. Двойной клик на бэнде «Данные»
2. Вкладка «Источник данных» → выбрать источник
3. Разместить объекты данных на бэнде

**Важно:** если бэнд «Данные» не подключён к источнику — в отчёте будет только одна строка.

---

## Группировка данных

Группировка объединяет данные по условию (например, по подразделению).

```
Заголовок группы  ← условие группировки (поле)
  Данные          ← строки внутри группы
Подвал группы     ← итоги группы
```

**Настройка:**
1. Выбрать бэнд «Данные» → «Добавить» → «Заголовок группы»
2. Двойной клик на «Заголовок группы» → задать поле группировки
3. Задать сортировку: не сортировать / по возрастанию / по убыванию

---

## Объекты макета

| Объект | Назначение |
|--------|-----------|
| Текст | Любой текст или выражение |
| Рисунок | Изображения (BMP, PNG, JPG, GIF, TIFF) |
| Таблица | Фиксированная сетка (число строк/столбцов известно заранее) |
| Матрица | Динамическая таблица (pivot-таблица) |
| Штрихкод | Штриховые коды |
| Флажок | Отображение boolean |
| Диаграмма MS Chart | Графики из данных отчёта |
| Вложенный отчёт | Дополнительный подмакет |

### Выражения в объектах

Выражения заключаются в квадратные скобки `[...]`:

```
// Системные переменные.
[Page]        // Номер страницы
[TotalPages]  // Всего страниц
[Date]        // Дата генерации

// Поля источника данных.
[Employees.Name]

// Параметры отчёта.
[BeginDate]

// Комбинация текста и выражений.
за период с [BeginDate] по [EndDate]

// Произвольный C# код.
[Hyperlinks.Get([DocumentRegister]).ToLower()]
```

### Системные переменные

| Переменная | Тип | Описание |
|------------|-----|----------|
| `Date` | DateTime | Дата и время генерации |
| `Page` | int | Номер текущей страницы |
| `TotalPages` | int | Всего страниц |
| `PageN` | string | «Страница N» |
| `PageNofM` | string | «Страница N из M» |
| `Row#` | int | Номер строки в группе (сброс при новой группе) |
| `AbsRow#` | int | Абсолютный номер строки |

### Условное форматирование

```
// Условие (выражение, возвращающее bool).
[DocumentReturningData.OverdueDelay] > 0

// При истинности меняются стиль: рамка, заливка, цвет текста, шрифт, видимость.
```

---

## Отчёт «Главный — подчинённый» (Master-Detail)

Отображает данные из связанных источников: одна строка главного → несколько строк подчинённого.

```
Данные (OurCompanies)         ← организация
  Данные (RegistrationSettings)  ← настройки регистрации этой организации
```

**Макет:**
1. Выбрать бэнд «Данные» → «Добавить» → «Данные» (вложенный)
2. Внешний бэнд → источник `OurCompanies`
3. Внутренний бэнд → источник `RegistrationSettings`
4. На внешнем — поля организации
5. На внутреннем — поля настроек

---

## Программная генерация отчёта

### API отчётов

| Метод / Свойство | Описание | Где доступен |
|------------------|----------|-------------|
| `report.Open()` | Открыть отчёт в просмотрщике | Клиент |
| `report.ExportToFile(fileName)` | Сохранить в файл | Клиент |
| `report.Export()` | Экспорт (возвращает `Stream`) | Сервер |
| `report.ExportTo(streamImporter)` | Экспорт в документ/версию | Сервер |
| `report.ExportFormat` | Формат: `Pdf`, `Word`, `Excel` | Оба |
| `report.CanExecute()` | Проверить права на выполнение | Оба |
| `Reports.ShowAll()` | Показать список отчётов модуля | Клиент |

### Получение отчёта

```csharp
// Отчёт модуля.
var report = Sungero.Docflow.Reports.GetApprovalSheetReport();

// Установить параметры.
report.Document = _obj;
report.BeginDate = Calendar.Today.AddMonths(-1);
```

### Пример: сохранить отчёт как версию документа

```csharp
// Серверная функция.
public virtual void GetApprovalSheet()
{
  var report = Sungero.Docflow.Reports.GetApprovalSheetReport();
  report.Document = _obj;

  // Экспорт в новую версию документа.
  report.ExportTo(_obj);
  _obj.Save();
}

// Клиентское действие.
public virtual void AddApprovalSheet(Sungero.Domain.Client.ExecuteActionArgs e)
{
  Functions.Contract.Remote.GetApprovalSheet(_obj);
}
```

### Пример: открыть отчёт в просмотрщике

```csharp
// Клиентский код.
var report = Sungero.MyModule.Reports.GetMyReport();
report.BeginDate = dateFrom;
report.EndDate = dateTo;
report.Open();
```

### Пример: экспорт в Stream на сервере

```csharp
[Remote]
public virtual Stream GenerateReport(DateTime from, DateTime to)
{
  var report = Sungero.MyModule.Reports.GetMyReport();
  report.BeginDate = from;
  report.EndDate = to;
  report.ExportFormat = ReportExportFormat.Pdf;
  return report.Export();
}
```

---

## Форматы экспорта

| Значение | Описание |
|----------|----------|
| `ReportExportFormat.Pdf` | PDF |
| `ReportExportFormat.Word` | Microsoft Word |
| `ReportExportFormat.Excel` | Microsoft Excel |

---

## Рекомендации для Linux

| Проблема | Решение |
|----------|---------|
| Неправильное отображение шрифтов | Установить на сервер те же шрифты, что и на клиентах |
| Лишние пустые строки в таблицах | Снять «Может разрываться» (CanBreak) со всех бэндов |
| Текст обрезается / переносится | Добавить ~1 символ к ширине столбцов |

---

## Права на отчёты

```csharp
// Тип права для отчётов.
DefaultReportAccessRightsTypes.Execute

// Проверка прав.
if (report.CanExecute())
  report.Open();
```

---

## Копирование отчётов

- Отчёт типа сущности можно скопировать в тип сущности или модуль
- Отчёт модуля **нельзя** скопировать в тип сущности (нет параметра `Entity`)
- После копирования обновите ссылки на типы сущностей в коде

---

*Источники: sds_poriadok_vypolneniia_otcheta.htm · sds_sobytiia_otcheta.htm · sds_kak_sgenerirovat_otchet.htm · sds_nastroika_maketa_otcheta.htm · sds_istochniki_dannykh.htm · sds_dobavlenie_parametrov.htm · sds_dobavlenie_bendov.htm · sds_gruppirovka_dannykh.htm · sds_dobavlenie_obektov_otcheta.htm · sds_sozdanie_otcheta_tipa_glavnyi_podchinennyi.htm · Sungero.Reporting.Server.xml · Sungero.Reporting.ClientBase.xml · Sungero.Reporting.Shared.xml*
