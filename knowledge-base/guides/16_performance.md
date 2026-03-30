# Быстродействие и ограничения платформы

> Источник: `sds_bustrodeistvie.htm`, `sds_zaprosy_k_serveru_prilozhenii.htm`, `sds_poluchenie_bolshogo_obema_dannuh.htm`, `sds_zhadnaya_zagruzka_svoistv.htm`, `sds_prikladnue_cachi.htm`, `sds_cache_params.htm`, `sds_razreshennye_classy.htm`, `sds_zapreshchennye_classy.htm`, `sds_razreshennye_vozmozhnosti.htm`, `sds_zapreshchennye_vozmozhnosti.htm`, `sds_optimizaciya_vychisleniya_vozmozhnosti_vypolneniya_deistviy.htm`

---

## Целевые метрики

| Метрика | Целевое значение |
|---------|-----------------|
| Время выполнения операции | **≤ 3 секунды** |
| Время запуска отчёта | **≤ 14 секунд** |
| Таймаут веб-сервера | **5 минут** (автоматическое прерывание) |

**Таймаут 5 минут НЕ применяется, если:**
- В прикладном коде нет обращений к БД
- Открыто модальное окно или диалог (таймер приостанавливается)

**Не рекомендуется** отключать таймаут — это отключает защиту от долгих запросов.

---

## Оптимизация запросов к серверу

### Рекомендуемое число запросов

| Операция | Запросов к серверу |
|----------|-------------------|
| Выделение записи в списке | **0** |
| Обновление формы (Refresh) | **0** |
| Отображение формы (Shown) | **0** |
| Изменение значения элемента управления | **0** |
| CanExecute для действия | **0** |
| Создание из списка | **1** |
| Открытие карточки | **1** |
| Сохранение карточки / выполнение действия | **≤ 5** |

**«0 запросов»** означает: в этих обработчиках **НЕЛЬЗЯ** вызывать Remote-функции или выполнять длительные операции. Они вызываются часто.

### Кэширование Remote-вызовов

Если Remote-функция используется в CanExecute или Refresh — среда выдаёт предупреждение при сборке. Результат кэшируется на время открытия карточки. Кэш сбрасывается при обновлении или закрытии формы.

### Логирование

Если клиентское событие с Remote-вызовом обрабатывается на сервере дольше **1 минуты** — это логируется в журнале веб-сервера.

Если запрос возвращает более **1000 записей** — это логируется по умолчанию.

---

## Жадная загрузка свойств (Eager Loading)

### Проблема N+1

При получении списка сущностей свойства могут загружаться:
1. **Сразу** (eager) — один запрос
2. **Лениво** (lazy) — отдельный запрос на каждую сущность = N+1 запросов

### Настройка

В редакторе типа сущности для свойства установить флажок **«Загружать значение сразу»**.

### Пример

Свойство `IsHeldByCounterParty` не отображается в колонках списка, но используется в `CanExecute`:

```csharp
public virtual bool CanReturnFromCounterparty(
    Sungero.Domain.Client.CanExecuteActionArgs e)
{
  return _obj.AccessRights.CanUpdate()
      && _obj.AccessRights.CanRegister()
      && _obj.IsHeldByCounterParty == true;
}
```

Без eager loading: N дополнительных запросов при проверке доступности действия.
С eager loading: 0 дополнительных запросов.

---

## Работа с IQueryable и большими объёмами данных

### Правила

- Используйте параметризованный `IQueryable<T>` для потенциально больших выборок
- Данные загружаются порциями; обработка начинается без ожидания полной загрузки
- `ToList()` — только для маленьких отфильтрованных коллекций

### Оптимизация: выбирайте только нужные свойства

```csharp
// ПЛОХО — загружает все свойства всех сущностей.
var docs = OfficialDocuments.GetAll().ToList();
foreach (var doc in docs)
  Logger.Debug(doc.Name);

// ХОРОШО — загружает только Name.
var names = OfficialDocuments.GetAll().Select(d => d.Name);

// ХОРОШО — несколько свойств через структуру.
var info = electronicDocument.Relations
    .GetRelatedAndRelatedFromDocuments()
    .Select(d => new DocumentInfo() { Id = d.Id, Name = d.Name })
    .ToList();
```

### Запрещённые LINQ-методы для репозиториев сущностей

```
.Concat()        .Distinct()       .GroupBy(p => p.Id)
.Last()          .LastOrDefault()   .Max(p => p.Number)
.OfType<T>()     .SelectMany()      .Sum(p => p.Number)
.Union()
```

Эти методы **запрещены** при обращении к коллекциям сущностей через GetAll(). Используйте их только после `.ToList()`.

---

## Кэширование

### Серверный кэш (Sungero.Core.Cache)

Только серверный код. Хранит простые типы или структуры.

```csharp
// Попытка получить из кэша.
Shell.Structures.Module.MyCache cachedData;
if (Cache.TryGetValue(cacheKey, out cachedData))
{
  // Используем кэш.
  return cachedData;
}

// Вычисляем заново.
var newData = ComputeExpensiveData();

// Сохраняем в кэш на 90 дней.
Cache.AddOrUpdate(cacheKey, newData, Calendar.Today.AddDays(90));
return newData;
```

**Методы:**

| Метод | Описание |
|-------|----------|
| `Cache.TryGetValue(key, out value)` | Получить значение |
| `Cache.AddOrUpdate(key, value, expiry)` | Добавить/обновить с TTL |
| `Cache.Remove(key)` | Удалить из кэша |

**Важно:** НЕ кэшируйте данные, которые нельзя восстановить или пересчитать.

### Кэширование через Params

Для сокращения серверных вызовов между событиями формы:

```csharp
// Сохранить в Refresh.
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
  if (_obj.DocumentKinds.Any() && _obj.State.Properties.DocumentKinds.IsChanged)
  {
    var condition = Functions.Condition.Remote.CreateCondition();
    var possibleConditions = Functions.ConditionBase.GetSupportedConditions(condition);
    e.Params.AddOrUpdate(
        Constants.ApprovalRule.IsSupportConditions,
        _obj.DocumentKinds.Any(x =>
            possibleConditions.ContainsKey(x.DocumentKind.DocumentType.DocumentTypeGuid)));
  }
}

// Прочитать в CanExecute (без Remote-вызова).
public override bool CanChartAddCondition(
    Sungero.Domain.Client.CanExecuteActionArgs e)
{
  bool isSupportConditions;
  if (e.Params.TryGetValue(
          Constants.ApprovalRule.IsSupportConditions,
          out isSupportConditions)
      && !isSupportConditions)
    return false;
  return true;
}
```

**Внимание:** при нажатии «Отменить изменения» все Params очищаются. Для сохранения значений — восстанавливайте их в Refresh через `e.Params.Contains()`.

---

## Оптимизация CanExecute для дочерних коллекций

По умолчанию для системных действий в дочерних коллекциях CanExecute проверяется **один раз на всю коллекцию**, а не для каждой записи. Это ускоряет работу с большими табличными частями.

Новые действия в дочерних коллекциях создаются с флажком **«Проверять возможность выполнения один раз для коллекции»**.

**Не использовать коллекционный CanExecute, если:**
- Действие — переключатель (toggle)
- Доступно без выделенных записей
- Нужна проверка для каждой записи отдельно

### Диагностика через логи

Поиск в логе: `"method":"child"`. Атрибут `durationMs > 500` → `isLong: true`.

---

## Разрешённые и запрещённые конструкции

### Разрешённые .NET-классы

| Класс / Тип | Примеры |
|-------------|---------|
| Простые типы + методы | `string.Format()`, `str.Trim()`, `int.Parse()` |
| `DateTime` (частично) | `date.AddDays()`, `date.ToShortDateString()` |
| `Dictionary<K,V>` | `dict.TryGetValue()` |
| `List<T>` | `list.Contains()`, `list.Add()` |
| `Array` | Массивы |
| `System.Linq` | Все extension-методы (с ограничениями для репозиториев) |
| `System.Math` | `Math.Round(value, 2)` |

### Запрещённые .NET-классы

| Запрещено | Альтернатива |
|-----------|-------------|
| `DateTime.Today`, `DateTime.Now` | `Calendar.Today`, `Calendar.Now` |
| `DateTime.Kind = Local` | Используйте серверное время |
| `System.Tuple` | Структуры (Structures) |
| `System.Globalization.CultureInfo` | `TenantInfo.Culture.SwitchTo()` |
| `System.Convert` | `int.Parse()`, `DateTime.Parse()` |
| `System.Reflection` (`typeof`) | `EntityType.Is()` |
| `System.Threading` | Async-обработчики |
| `System.Data` (ADO.NET) | `SQL.CreateConnection()` |
| `System.IO` (файлы) | Потоки через API |
| `System.Xml` | — |
| `System.Windows` | Диалоги Sungero |

### Запрещённые конструкции C#

| Запрещено | Альтернатива |
|-----------|-------------|
| `new { Name = "..." }` (анонимные типы) | Структуры |
| `entity is IEmployee` | `Employees.Is(entity)` |
| `entity as IEmployee` | `Employees.As(entity)` |
| `new Tuple<>()`, `new Exception()` | `Create()` методы |
| `static` поля | Свойства |
| `out` параметры | Структуры |
| `catch (Exception) { }` (подавление) | `catch (Exception) { throw; }` |

### Разрешённые конструкции C#

```csharp
// Исключения — создание и выброс.
throw AppliedCodeException.Create(Resources.Message);

// Исключения — перехват и повторный выброс.
try { ... }
catch (AppliedCodeException ex) { Logger.Error(ex.Message); throw; }

// Структуры и константы — через группу «Структуры и константы».
// Коллекции — через new.
var list = new List<string>();
var dict = new Dictionary<string, int>();
```

---

## Классы Sungero — разрешения

| Разрешено | Запрещено |
|-----------|----------|
| Все прикладные модули (`Sungero.Company`, `Sungero.Docflow`, ...) | Все остальные `Sungero.*` |
| Предметные модули (`Sungero.Content`, `Sungero.Workflow`) | Системные пространства имён |
| `Sungero.Core` | — |
| `Sungero.CoreEntities` | — |

---

## Способы ускорения

| Способ | Описание |
|--------|----------|
| Индексы | SQL-индексы для тяжёлых запросов |
| Прикладные кэши | `Cache.AddOrUpdate()` для повторных вычислений |
| `AccessRights.AllowRead()` | Отключение проверки прав (серверный код) |
| Передача отфильтрованных данных | Фильтрация на сервере, не на клиенте |
| IQueryable | Порционная загрузка больших объёмов |
| Жадная загрузка | Флажок «Загружать значение сразу» |
| Объединение мелких серверных вычислений | Одна Remote-функция вместо нескольких |
| Async-обработчики | Вынос тяжёлых операций из основного потока |
| Params | Кэширование результатов между событиями формы |

---

## Инструменты профилирования

- Анализ лог-файлов (журнал веб-сервера)
- dotTrace — хронометраж действий на веб-сервере
- Встроенные средства СУБД (EXPLAIN, pg_stat_statements)
- Техническое решение «Directum RX Мониторинг»

---

*Источники: sds_bustrodeistvie.htm · sds_zaprosy_k_serveru_prilozhenii.htm · sds_poluchenie_bolshogo_obema_dannuh.htm · sds_zhadnaya_zagruzka_svoistv.htm · sds_prikladnue_cachi.htm · sds_cache_params.htm · sds_razreshennye_classy.htm · sds_zapreshchennye_classy.htm · sds_razreshennye_vozmozhnosti.htm · sds_zapreshchennye_vozmozhnosti.htm · sds_optimizaciya_vychisleniya_vozmozhnosti_vypolneniya_deistviy.htm*
