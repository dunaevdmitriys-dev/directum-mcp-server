---
id: ds_sobytiia_sushchnosti
module: ds
role: Developer
topic: Серверные события сущности
breadcrumb: "Разработка > Программный код > События типов сущностей"
description: "Какие есть, существуют события сущности. До сохранения (BeforeSave). До сохранения в транзакции (Saving). После сохранения в транзакции (Saved). После сохранения (AfterSave). До сохранения истории (BeforeSaveHistory). До удаления (BeforeDelete). До удаления в транзакции (Deleted). После удаления (AfterDelete). Копирование (CreatingFrom). Создание (Created). Фильтрация (Filtering)"
source: webhelp/WebClient/ru-RU/ds_sobytiia_sushchnosti.htm
---

# Серверные события сущности

События сущности задаются в редакторе типа сущности в группе «Серверные события».

Серверные события
События, которые есть у всех типов сущностей
До сохранения | BeforeSave
До сохранения (в транзакции) | Saving
После сохранения (в транзакции) | Saved
После сохранения | AfterSave
До сохранения истории | BeforeSaveHistory
До удаления | BeforeDelete
До удаления (в транзакции) | Deleting
После удаления | AfterDelete
Копирование | CreatingFrom
Создание | Created
Предварительная фильтрация | PreFiltering
Фильтрация | Filtering
События только для типов документов
До подписания | BeforeSigning
Смена типа | ConvertingFrom
События только для типа задачи
До старта | BeforeStart
До рестарта | BeforeRestart
До возобновления | BeforeResume
До прекращения | BeforeAbort
После приостановки | AfterSuspend
События только для типа задания
До выполнения | BeforeComplete
События только для задания на приемку
До принятия | BeforeAccept
До отправки на доработку | BeforeSendForRework
События только для конкурентного задания
До взятия в работу | BeforeStartWork
До возврата невыполненным | BeforeReturnUncompleted
События только для типа справочника
UI-фильтрация | UIFiltering
Показ подсказки (только для наследников Sungero.CoreEntities.User ) | GetDigest

## До сохранения (BeforeSave)

```csharp
// Событие «До сохранения» группы регистрации.
public override void BeforeSave(Sungero.Domain.BeforeSaveEventArgs e)
{
var responsible = _obj.State.Properties.ResponsibleEmployee.OriginalValue;
var isResponsible = (responsible == null || Recipients.AllRecipientIds.Contains(responsible.Id)) && _obj.AccessRights.CanUpdate();
// Если сотрудник не является ответственным за группу регистрации
// и у него нет прав на выполнение операции «Управление правами доступа»,
// то выводить сообщение "Недостаточно прав для изменения группы регистрации".
if (!isResponsible && !_obj.AccessRights.CanManage())
e.AddError(RegistrationGroups.Resources.EnoughRights);
}
```

## До сохранения (в транзакции) (Saving)

```csharp
// Выдать права участникам на повестку совещания.
public override void Saving(Sungero.Domain.SavingEventArgs e)
{
base.Saving(e);
PublicFunctions.Meeting.SetAccessRightsOnDocument(_obj.Meeting, _obj);
}
```

## После сохранения (в транзакции) (Saved)

```csharp
// Если в нашей организации изменился руководитель, то у предыдущего руководителя
// удалить системные замещения.
public override void Saved(Sungero.Domain.SavedEventArgs e)
{
if (_obj.State.Properties.CEO.IsChanged &&
_obj.State.Properties.CEO.OriginalValue != null)
Functions.Module.DeleteSystemSubstitutions(managers, _obj.State.Properties.CEO.OriginalValue);
}
```

## После сохранения (AfterSave)

```csharp
// Сотруднику, указанному в карточке документа в поле «Ответственный»
// (Responsible), выдать права на чтение этого документа.
public override void AfterSave(Sungero.Domain.AfterSaveEventArgs e)
{
// Проверить, есть ли у сотрудника права на чтение документа.
if (!_obj.AccessRights.CanRead(_obj.Responsible))
// Если нет – выдать.
_obj.AccessRights.Grant(_obj.Responsible, DefaultAccessRightsTypes.Read);
}
```

## До сохранения истории (BeforeSaveHistory)

```csharp
// Добавить отдельную запись регистрации в историю работы при создании документа.
public override void BeforeSaveHistory(Sungero.Content.DocumentHistoryEventArgs e)
{
var isCreateAction = e.Action == Sungero.CoreEntities.History.Action.Create;
var operation = new Enumeration(Constants.OfficialDocument.Operation.Registration);
var operationDetailed = new Enumeration(Constants.OfficialDocument.OperationDetailed.Registration);
var comment = !string.IsNullOrWhiteSpace(_obj.RegistrationNumber) ?
string.Join("|", _obj.RegistrationNumber, _obj.DocumentRegister) :
string.Empty;
if (isCreateAction && _obj.RegistrationState != RegistrationState.NotRegistered)
e.Write(operation, operationDetailed, comment, null);
}
```

## До удаления (BeforeDelete)

```csharp
// Отобразить сообщение об ошибке, если сущность уже подписана.
public override void BeforeDelete(BeforeDeleteEventArgs e)
{
if (_obj.IsSigned)
e.AddError("Нельзя удалять подписанный документ");
}
```

## До удаления (в транзакции) (Deleting)

Выполняется в SQL-транзакции перед удалением сущности из базы данных. Назначение • внесение изменений в связанные сущности; • выполнение дополнительной логики в транзакции при удалении сущности. Например, обращение к базе данных через SQL-запросы. Аргументы события • _obj – удаляемая сущность; • e.Params – дополнительные параметры.

## После удаления (AfterDelete)

```csharp
// В задаче на ознакомление с документом (AcquaintanceTask) удалить список
// участников ознакомления.
public override void AfterDelete(Sungero.Domain.AfterDeleteEventArgs e)
{
// Удалить список участников ознакомления, соответствующий задаче.
if (_obj != null)
{
var participants = AcquaintanceTaskParticipants.GetAll().FirstOrDefault(x => x.TaskId == _obj.Id);
if (participants != null)
AcquaintanceTaskParticipants.Delete(participants);
}
}
```

## Копирование (CreatingFrom)

```csharp
// При копировании организации запретить копировать комментарий.
public override void CreatingFrom(Sungero.Domain.CreatingFromEventArgs e)
{
base.CreatingFrom(e);
e.Without(_info.Properties.IsCardReadOnly);

// Организацию нельзя редактировать, если она создана как копия нашей
// организации. При копировании такой организации, комментарий в новую
// переноситься не должен.
if (_source.IsCardReadOnly == true)
e.Without(_info.Properties.Note);
}
```

## Создание (Created)

```csharp
// Запретить создание карточки справочника "Персоны"
// из любых мест, кроме карточки справочника "Контакты".
public override void Created(Sungero.Domain.CreatedEventArgs e)
{
if (!CallContext.CalledFrom(Contacts.Info))
throw new InvalidOperationException("Создавать персону можно только из карточки контакта.");

}
```

## Предварительная фильтрация (PreFiltering)

Выполняется каждый раз при получении сущности или списка сущностей. Назначение Используется для оптимизации выполнения SQL-запроса к большому списку сущностей с панелью фильтрации. Например, предварительную фильтрацию можно использовать для ограничения выборки данных, если список содержит более 10 млн записей. Чтобы выполнить запрос к базе данных оптимально, при добавлении события соблюдайте условия: • критерии фильтрации события Предварительная фильтрация должны значительно сократить объем данных; • для выбранных критериев фильтрации в базе данных созданы индексы; • критерии не должны фильтровать список по логическим свойствам. Вместо этого, например, задайте предварительную фильтрацию по подразделению или организации. Если событие Предварительная фильтрация задано, то SQL-запрос выполняется в два этапа: • событие Предварительная фильтрация формирует запрос на получение выборки данных по базовым критериям. В результате список записей ограничивается для дальнейшей фильтрации; • событие Filtering фильтрует получившийся список по остальным критериям. В коде события Предварительная фильтрация можно сформировать содержимое списка по часто используемым критериям, например отфильтровать реестр договоров по периоду. При этом в выборку попадают только записи, на которые у пользователя есть права доступа. Затем запрос из события Filtering выполняется к уменьшенному объему данных. События формируют один общий запрос вместо двух, если выполняется одно из условий: • вычисления перенесены с веб-сервера, например на сервис асинхронных событий; • в событии Предварительная фильтрация не заданы фильтры; • в списке нет панели фильтрации; • используется СУБД Microsoft SQL Server. Возвращаемое значение Измененный запрос на получение списка сущностей. По умолчанию возвращается исходный запрос. Аргументы события : • query – исходный запрос на получение списка сущностей; • e.Params – дополнительные параметры; • _createFromTemplateContext – документ, для которого создается версия из шаблона. Аргумент отображается только в шаблонах документов, наследниках от Sungero.Content.ElectronicDocumentTemplate . Подробнее см. в разделе «Как отфильтровать список шаблонов, доступных при создании документа» . ПРИМЕЧАНИЕ . Не рекомендуется использовать оператор OR (ИЛИ) в коде обработчика события фильтрации, если предполагается, что запрос будет обращаться к большим спискам (более 1 млн. записей).

## Фильтрация (Filtering)

```csharp
// При создании документа фильтровать список шаблонов.
public override IQueryable<T> Filtering(IQueryable<T> query, Sungero.Domain.FilteringEventArgs e)
{
if (_createFromTemplateContext != null)
{
query = query.Where(d => d.Status == Status.Active);
if (Docflow.OfficialDocuments.Is(_createFromTemplateContext))
{
var document = Docflow.OfficialDocuments.As(_createFromTemplateContext);
query = query.Where(template => template.DocumentKind.Equals(document.DocumentKind));
}
}
return query;
}
```

## До подписания (BeforeSigning)

```csharp
// Для кадровых документов принудительно устанавливать формат CAdES-A.
public override void BeforeSigning(Sungero.Domain.BeforeSigningEventArgs e)
{
e.SignatureTargetFormat = SignatureAutoImproveTargetFormat.CadesA;
}
```

## Смена типа (ConvertingFrom)

```csharp
// Сменить тип входящего документа электронного обмена.
public override void ConvertingFrom(Sungero.Domain.ConvertingFromEventArgs e)
{
base.ConvertingFrom(e);

if (ExchangeDocuments.Is(_source) &&
OfficialDocuments.As(_source).LifeCycleState != OfficialDocument.LifeCycleState.Obsolete)
e.Without(_info.Properties.LifeCycleState);
}
```

## До старта (BeforeStart)

```csharp
// При старте задачи проверить, что документы вложены в группу «DocumentsForSign».
public static void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)
{
var attachmentsAddedHere = _obj.DocumentsForSign.Where(d => _obj.AttachmentsInfo.Any(i => i.Target == entity && i.LinkedTo(a));

if (!attachmentsAddedHere.Any())
e.AddError("Вложите документы на подписание");
}
```

## До рестарта (BeforeRestart)

```csharp
// При рестарте задачи на исполнение поручения (ActionItemExecutionTask) очистить
// ее причину прекращения и статус.
public override void BeforeRestart(Sungero.Workflow.Server.BeforeRestartEventArgs e)
{
_obj.AbortingReason = string.Empty;
_obj.ExecutionState = null;
}
```

## До возобновления (BeforeResume)

```csharp
// При возобновлении задачи проверить, что документы вложены в группу «DocumentsForSign».
public override void BeforeResume(Sungero.Workflow.Server.BeforeResumeEventArgs e)
{
base.BeforeResume(e);
Functions.ApprovalTask.RevokeAuthorAccessRights(_obj);
}
```

## До прекращения (BeforeAbort)

```csharp
// Восстановить полные права автора на документ при прекращении задачи.
public override void BeforeAbort(Sungero.Workflow.Server.BeforeAbortEventArgs e)
{
base.BeforeAbort(e);
Functions.ApprovalTask.RestoreAuthorAccessRights(_obj);
}
```

## После приостановки (AfterSuspend)

Выполняется после приостановки задачи из-за ошибки выполнения. Назначение Выполнение некоторой логики при возникновении ошибки. Например, отправка уведомления об ошибке. Аргументы события • _obj – задача; • e.Params – дополнительные параметры.

## До выполнения (BeforeComplete)

```csharp
// Проверка полномочий сотрудника при согласовании задания.
public override void BeforeComplete(Sungero.Workflow.Server.BeforeCompleteEventArgs e)
{
base.BeforeComplete(e);
if (_obj.Result == Result.Approved)
{
var stage = ApprovalStages.As(_obj.Stage);
if (stage != null && stage.IsCheckAuthority == true)
{
var str = Purchase.PublicFunctions.GetInfoAboutAuthority(Sungero.Company.Employees.Current, _obj.DocumentGroup.OfficialDocuments.FirstOrDefault());
if (!string.IsNullOrEmpty(str))
e.Result = Purchase.Resources.CheckAuthorityAssignmentFormat(e.Result, str);
}
}
}
```

## До принятия (BeforeAccept)

Выполняется перед принятием задания на приемку. Назначение • валидация задания – проверка возможности его выполнения, проверка заполнения полей; • заполнение текста или свойств информацией из связанных сущностей или вложений. Аргументы события • _obj – задание на приемку, которое принимается; • e.AddError() – добавление сообщения об ошибке. При этом выполняемые операции прерываются; • e.AddInformation() – добавление информационного сообщения. При этом выполняемые операции не прерываются; • e.AddWarning() – добавление текста с предупреждением. При этом выполняемые операции не прерываются. Примечание. Сообщение об ошибке, информационное сообщение и предупреждение отображаются в верхней части формы карточки с соответствующей иконкой. Например, информационное сообщение выглядит следующим образом: • e.IsValid – признак того, что задача валидная. Возможные значения: • True . Задача валидная и доступна для прекращения; • False . Задача не валидная. При старте задачи появится сообщение из аргумента e.AddError(). • e.Params – дополнительные параметры.

## До отправки на доработку (BeforeSendForRework)

Выполняется перед отправкой на доработку задания на приемку. Назначение • валидация задания – проверка возможности его выполнения, проверка заполнения полей; • заполнение текста или свойств информацией из связанных сущностей или вложений. Аргументы события • _obj – отправляемое на доработку задание на приемку; • e.AddError() – добавление сообщения об ошибке. При этом выполняемые операции прерываются; • e.AddInformation() – добавление информационного сообщения. При этом выполняемые операции не прерываются; • e.AddWarning() – добавление текста с предупреждением. При этом выполняемые операции не прерываются. Примечание. Сообщение об ошибке, информационное сообщение и предупреждение отображаются в верхней части формы карточки с соответствующей иконкой. Например, информационное сообщение выглядит следующим образом: • e.IsValid – признак того, что задача валидная. Возможные значения: • True . Задача валидная и доступна для прекращения; • False . Задача не валидная. При старте задачи появится сообщение из аргумента e.AddError(). • e.Params – дополнительные параметры.

## До взятия в работу (BeforeStartWork)

```csharp
// Если задание просрочено, при взятии в работу добавить к сроку 4 рабочих часа.
public override void BeforeStartWork(Sungero.Workflow.Server.BeforeStartWorkEventArgs e)
{
if (Docflow.PublicFunctions.Module.CheckDeadline(_obj.Performer, _obj.Deadline, Calendar.Now))
_obj.Deadline = Calendar.Now.AddWorkingHours(4);
}
```

## До возврата невыполненным (BeforeReturnUncompleted)

```csharp
public override void ReturnUnсompleted(Sungero.Domain.Client.ExecuteActionArgs e)
{
// Получить все проекты резолюции, добавленные пользователем в рамках задания.
var allActionItems = _obj.ResolutionGroup.ActionItemExecutionTasks.ToList();
var actionItemsToDelete = Functions.Module.Remote.GetActionItemsAddedToAssignment(_obj, allActionItems, Company.Employees.Current,
PublicConstants.PreparingDraftResolutionAssignment.ResolutionGroupName);
var hasActionItemsToDelete = actionItemsToDelete.Any();
var description = hasActionItemsToDelete ? Resources.ConfirmDeleteDraftResolutionAssignment : null;
var dropDialogId = hasActionItemsToDelete
? Constants.DocumentReviewTask.DocumentReviewAssignmentConfirmDialogID.ReturnUncompleted
: Constants.DocumentReviewTask.DocumentReviewAssignmentConfirmDialogID.ReturnUncompletedWithDeletingDraftResolutions;
var dropIsConfirmed = Docflow.PublicFunctions.Module.ShowConfirmationDialog(e.Action.ConfirmationMessage,description,null,dropDialogId);
if (!dropIsConfirmed)
{
e.CloseFormAfterAction = false;
return;
}

base.ReturnUnсompleted(e);
}
```

## UI-фильтрация (UIFiltering)

```csharp
public virtual IQueryable<T> OurSignatoryFiltering(IQueryable<T> query, Sungero.Domain.PropertyFilteringEventArgs e)
{
e.DisableUiFiltering = true;
return query;
}
```

## Показ подсказки (GetDigest)

```csharp
// Получить подсказку о сотруднике.
public override IDigestModel GetDigest(Sungero.Domain.GetDigestEventArgs e)
{
var digest = UserDigest.Create(_obj);
digest.Header = "Заголовок всплывающей подсказки";
digest.AddLabel("Текстовый контрол");
digest.AddEntity(_obj);
digest.AddEntity(_obj, "Заголовок сущности:");
digest.AddEntity(_obj, "Заголовок сущности:", "Имя сущности");
digest.AddHyperlink("https://mycompany.com");
digest.AddHyperlink("https://mycompany.com", "Имя ссылки");
digest.AddHyperlink("https://mycompany.com", "Имя ссылки", "Заголовок:");
return digest;
}
```

**См. также**

Валидация
