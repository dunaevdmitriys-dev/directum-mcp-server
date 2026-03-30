---
id: ds_zadanie_uslovii_filtratcii_vychisliaemykh_papok
module: ds
role: Developer
topic: Пример настройки фильтрации записей в папке
breadcrumb: "Разработка > Элементы разработки > Модуль > Папки"
description: "Как задать условия фильтрации в вычисляемой папке. Пример кода для фильтрации. Как проверить заполнение контролов на панели фильтрации. Валидация панели фильтрации. Как настроить валидацию панели фильтрации"
source: webhelp/WebClient/ru-RU/ds_zadanie_uslovii_filtratcii_vychisliaemykh_papok.htm
---

# Пример настройки фильтрации записей в папке

Текст раздела в разработке

Благодаря фильтрации записи в папке мгновенно скрывают ненужные файлы, позволяя сосредоточиться на нужных данных. Это помогает быстро находить и упорядочивать просмотр содержимого без перемещений файлов.

Предположим, в вычисляемой папке «Документы к возврату» добавлена панель фильтрации:

Необходимо настроить фильтрацию записей в папке. Для этого добавьте обработчики событий:

- Получение данных . В событии напишите логику вычисления содержимого папки: в ней должны отображаться заявка, обоснование и приложения документа. В этом же событии задайте условия для фильтрации содержимого папки по критериям на панели фильтрации;
- Проверка фильтра . В событии напишите код для валидации панели фильтрации. В проводнике должно появляться сообщение, если при фильтрации списка пользователь выбрал произвольный период оформления заявки и не заполнил критерии «с», «по», «В подразделении» и «У сотрудника». При таком сочетании критериев запрос на веб-сервере будет выполняться долго и вернет большое количество записей. То есть пользователю все равно придется уточнять критерии фильтрации.
- Примечание. Проверка фильтра работает только в веб-клиенте.

Чтобы добавить логику работы панели фильтрации:

- 1. В редакторе модуля перейдите на вкладку «Папки» и нажмите на кнопку :
- 2. Добавьте логику вычисления содержимого папки и условия фильтрации. Для этого в группе «События» перейдите к обработчику события Получения данных и нажмите на кнопку . В открывшемся редакторе напишите код обработчика.
- ВАЖНО. В начало обработчика добавьте проверку, что панель фильтрации включена. Для этого используйте аргумент _ filter .

```csharp
/// <summary>
/// Получить выданные документы.
/// </summary>
/// <param name="_filter">Фильтр.</param>
/// <returns>Список выданных документов.</returns>
public virtual IQueryable<Sungero.Docflow.IOfficialDocument> DocumentsToReturnDataQuery(IQueryable<Sungero.Docflow.IOfficialDocument> query)
{
// Проверка того, что панель фильтрации включена.
if (_filter == null)
return documents;

// Получение списка сущностей, которые должны отображаться
// в вычисляемой папке (регистрируемые и нумеруемые документы,
// которые подлежат возврату).

var documents = query;// Объявление переменой documents
var today = Calendar.UserToday;
var documents = query.Where(l => l.IsReturnRequired == true || l.IsHeldByCounterParty == true);

documents = documents.Where(d => !Docflow.ContractualDocumentBases.Is(d) && !Docflow.AccountingDocumentBases.Is(d));

// Фильтр по статусу.
if (_filter.Overdue)
{
documents = documents.Where(l => l.Tracking.Any(d => d.ReturnDate > d.ReturnDeadline || (!d.ReturnDate.HasValue && d.ReturnDeadline < today)));
}

// Фильтр по виду документа.
if (_filter.DocumentKind != null)
{
documents = documents.Where(l => Equals(l.DocumentKind, _filter.DocumentKind));
}

// Фильтр по сотруднику.
if (_filter.Employee != null)
{
documents = documents.Where(l => l.Tracking.Any(d => Equals(d.DeliveredTo, _filter.Employee)));
}

// Фильтр по подразделению.
if (_filter.Department != null)
{
documents = documents.Where(l => l.Tracking.Any(d => Equals(d.DeliveredTo.Department, _filter.Department)));
}

// Фильтр по группе регистрации.
if (_filter.RegistrationGroup != null)
{
documents = documents.Where(l => l.DocumentRegister != null &&
Equals(l.DocumentRegister.RegistrationGroup, _filter.RegistrationGroup));
}

// Фильтр по делу.
if (_filter.Filelist != null)
{
documents = documents.Where(l => Equals(l.CaseFile, _filter.Filelist));
}

// Исключить строки с Результатом возврата: "Возвращен".
var returned = Docflow.OfficialDocumentTracking.ReturnResult.Returned;

// Фильтр по сроку возврата: до конца дня.
if (_filter.EndDay)
documents = documents.Where(l => l.Tracking.Any(p => !Equals(p.ReturnResult, returned) && p.ReturnDeadline < today.AddDays(1)));

// Фильтр по сроку возврата: до конца недели.
if (_filter.EndWeek)
documents = documents.Where(l => l.Tracking.Any(p => !Equals(p.ReturnResult, returned) && p.ReturnDeadline <= today.EndOfWeek()));

// Фильтр по сроку возврата: до конца месяца.
if (_filter.EndMonth)
documents = documents.Where(l => l.Tracking.Any(p => !Equals(p.ReturnResult, returned) && p.ReturnDeadline <= today.EndOfMonth()));

// Фильтр по сроку возврата: в период с, по.
if (_filter.Manual)
{
var dateFrom = _filter.ReturnPeriodDataRangeFrom;
var dateTo = _filter.ReturnPeriodDataRangeTo;

if (dateFrom.HasValue && !dateTo.HasValue)
documents = documents.Where(l => l.Tracking.Any(p => !Equals(p.ReturnResult, returned) && p.ReturnDeadline >= dateFrom.Value));

if (dateTo.HasValue && !dateFrom.HasValue)
documents = documents.Where(l => l.Tracking.Any(p => !Equals(p.ReturnResult, returned) && p.ReturnDeadline <= dateTo.Value));

if (dateFrom.HasValue && dateTo.HasValue)
documents = documents.Where(l => l.Tracking.Any(p => !Equals(p.ReturnResult, returned) && (p.ReturnDeadline >= dateFrom.Value && p.ReturnDeadline <= dateTo.Value)));
}

return documents;
}
```

- 3. Чтобы добавить валидацию панели фильтрации, перейдите в группу «События» к обработчику события Проверка фильтра и нажмите на кнопку . В открывшемся редакторе напишите код обработчика:

```csharp
partial class DocumentsToReturnFolderHandlers
{
/// <summary>
/// Проверка фильтра для списка выданных документов.
/// </summary>
public override void DocumentsToReturnValidateFilterPanel(Sungero.Domain.Client.ValidateFilterPanelEventArgs e)
{
// Не выполнять фильтрацию, если установлен
// произвольный период возврата документов
// и не заполнены критерии "с", "по" "В подразделении" и "У сотрудника".
if (_filter.Manual &&
_filter.Department== null &&
_filter.Employee == null &&
_filter.Info.ReturnPeriodDataRangeFrom == null &&
_filter.Info.ReturnPeriodDataRangeTo == null)
e.AddError("Заполните параметры, чтобы сократить число результатов поиска", _filter.Info.Department, _filter.Info.Employee, _filter.Info.ReturnPeriodDataRangeFrom, _filter.Info.ReturnPeriodDataRangeTo);
}
}
```

- 4. Опубликуйте и проверьте разработку.

В результате в папке отображается регистрируемые и нумеруемые документы, которые пришли на согласование.

**См. также**

Настройка панели фильтрации Добавление набора флажков Добавление навигации Добавление периода дат Пример задания условий фильтрации в списке Как настроить валидацию панели фильтрации для входящих документов
