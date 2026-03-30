---
id: ds_funktcii
module: ds
role: Developer
topic: Функции
breadcrumb: "Разработка > Программный код"
description: "Как работать с функциями. Функции инициализации. Где создаются функции инициализации. Для чего нужны функции инициализации. Для чего нужны клиентские, серверные, разделяемые функции"
source: webhelp/WebClient/ru-RU/ds_funktcii.htm
---

# Функции

Функции используются для:

- повторного использования кода в нескольких местах, что позволяет не дублировать логику;
- повышения читаемости кода, так как действия логически группируются.

В среде разработки функции создаются в редакторах:

• модулей, когда предполагается, что в дальнейшем одна и та же логика может выполняться в нескольких типах сущностей, асинхронных обработчиках или фоновых процессах модуля;

• типов сущностей, когда предполагается, что в дальнейшем они могут вызываться в разных местах программного кода. Например, в других функциях этого же типа сущности или событиях.

Функции делятся на:

• клиентские ;

• серверные (в том числе с атрибутами Remote и Converter );

• разделяемые .

Серверные и разделяемые функции модулей можно сделать интеграционными .

Также есть функции инициализации , вычисляемых выражений , контрола состояния и валидации для правил ввода .

Совет. Чтобы обратиться к сущности, в классе которой создается функция без модификатора static , используется параметр _obj . Например, если функция создается в типе документа OutgoingDocumentBase (Исходящий документ), то в ее коде можно обратиться к свойству SentDate (Дата отправки) с помощью кода _obj.SentDate .

## Клиентские функции

Могут вызываться только из клиентского кода.

Функции, которые можно выбирать при настройке действий . Для настройки доступны клиентские функции с типом возвращаемого значения void , List<IEntity> , IQuieryable<IEntity> .

Совет. Чтобы в редакторе обложек отображалось локализованное значение имени и описания действия,добавьте атрибут LocalizeFunction в клиентскую функцию.

Пример. Получение настроек видимости оргструктуры

Предположим, что нужно показывать настройки видимости организационной структуры. Для этого в модуле Company (Компания) создается клиентская функция:

```csharp
/// <summary>
/// Показать настройки видимости организационной структуры.
/// </summary>
public virtual void ShowVisibilitySettings()
{
if (!VisibilitySettings.AccessRights.CanUpdate())
{
Dialogs.ShowMessage(Resources.VisibilitySettingsNotAvailable);
return;
}
Functions.Module.Remote.GetVisibilitySettings().Show();
}
```

## Серверные функции

Могут вызываться только из серверного кода.

Пример. Получение типа счета

Предположим, что перед созданием счета нужно определить его тип. Для этого в модуле Docflow (Документооборот) создается серверная функция:

```csharp
//// <summary>
/// Получить тип счета.
/// </summary>
/// <param name="document">Документ.</param>
/// <param name="versionId">ИД версии.</param>
/// <returns>Тип счета.</returns>
public virtual string GetInvoiceType(IOfficialDocument document, long versionId)
{
if (!Contracts.IncomingInvoices.Is(document))
return null;

var version = document.Versions.Single(v => v.Id == versionId);
if (version.BodyAssociatedApplication.Extension != Extensions.Xml)
return null;

var xml = GetNullableXmlDocument(version.Body.Read());
return Exchange.PublicFunctions.Module.GetInvoiceType(xml, null);
}
```

## Серверные функции с атрибутом Remote

Создаются в серверном коде, в дальнейшем могут вызываться в клиентском коде. Такие функции используются для получения, создания и удаления сущностей. В качестве параметров и типа возвращаемого значения для серверных функций с атрибутом Remote используются только:

- простые типы:

short, short? | Guid, Guid? | double, double?
int, int? | bool, bool? | Uri
long, long? | decimal, decimal? | string
char, char? | DateTime, DateTime?

- типы сущностей. Например, IEmployee , ICompany , IContract ;
- список List, содержащий простые типы или сущности. Например, List<int> , List<ICompany> . Используется, если функция возвращает небольшой список;
- параметризованный тип IQueryable (запрос на получение данных). Содержит только сущности. Например, IQueryable<IContractualDocument> . Используется при работе с большими объемами данных. В этом случае данные с сервера подгружаются порциями. Функция вызывается при загрузке каждой порции. Это позволяет работать с данными без ожидания полной загрузки с сервера.
- важно. В серверных функциях не рекомендуется выполнять длительные расчеты. Также не рекомендуется часто вызывать серверные функции из клиентского кода.

Пример. Создание документа по проекту

Предположим, что нужно создать документ по проекту. Для этого в справочнике ProjectCore (Реестр проектов) создается серверная функция:

```csharp
/// <summary>
/// Создать документ по проекту.
/// </summary>
/// <returns>Документ.</returns>
[Remote]
public IOfficialDocument CreateProjectDocument()
{
var document = ProjectDocuments.Create();
document.Project = _obj;
return document;
}
```

Затем функцию необходимо вызвать в клиентском коде, например, в действии CreateProjectDocument справочника ProjectCore (Реестр проектов):

```csharp
/// <summary>
/// Обработчик выполнения действия "Создать документ".
/// </summary>
public virtual void CreateProjectDocument(Sungero.Domain.Client.ExecuteActionArgs e)
{
var document = Functions.ProjectCore.Remote.CreateProjectDocument(_obj);
document.Show();
}
```

## Серверные функции с атрибутом Converter

Создаются в серверном коде. Функции с атрибутом Converter используются для преобразования одного объекта в другой по заданной логике.

Пример. Получение суммы договора прописью с валютой

Предположим, что для заполнения шаблона договорного документа нужно получить его сумму прописью с валютой. Для этого в типе документа ContractualDocument (Договорной документ) создается серверная функция:

```csharp
/// <summary>
/// Получить для договорного документа сумму прописью с валютой.
/// </summary>
/// <param name="contractualDocument">Договорной документ.</param>
/// <returns>Сумма прописью с валютой.</returns>
[Converter("TotalAmountInCurrencyToWords")]
public static string TotalAmountInCurrencyToWords(IContractualDocument contractualDocument)
{

if (contractualDocument.TotalAmount == null || contractualDocument.Currency == null)
return null;

return Docflow.PublicFunctions.Module.GetAmountWithCurrencyInWords(contractualDocument.TotalAmount.Value, contractualDocument.Currency);
}
```

## Разделяемые функции

Могут вызываться только из разделяемого кода в событиях «Изменение значения свойства» и «Обновление формы» .

Пример. Обязательность заполнения свойства RegistrationGroup (Группа регистрации) в справочнике DocumentRegister (Журналы регистрации)

Предположим, что в справочнике DocumentRegister (Журналы регистрации) нужно установить обязательность заполнения для свойства RegistrationGroup (Группа регистрации), если у него установлен признак обязательности или в свойстве RegisterType (Тип журнала) указано значение «Регистрация». Для этого в справочнике DocumentRegister (Журналы регистрации) создается разделяемая функция:

```csharp
/// <summary>
/// Установить обязательность свойств в зависимости от заполненных данных.
/// </summary>
public virtual void SetRequiredProperties()
{
_obj.State.Properties.RegistrationGroup.IsRequired =
_obj.Info.Properties.RegistrationGroup.IsRequired ||
_obj.RegisterType == RegisterType.Registration;
}
```

Функцию необходимо вызвать при изменении свойства RegisterType (Тип журнала), а также при обновлении формы справочника, например, чтобы при отмене изменений корректно отрабатывалась смена доступности, обязательности и видимости свойств:

```csharp
/// <summary>
/// Обработчик события "Изменение значения свойства" свойства "Тип журнала".
/// </summary>
public virtual void RegisterTypeChanged(Sungero.Domain.Shared.EnumerationPropertyChangedEventArgs e)
{
Functions.DocumentRegister.SetRequiredProperties(_obj);
}
```

```csharp
/// <summary>
/// Обработчик события "Обновление формы".
/// </summary>
public override void Refresh(Sungero.Presentation.FormRefreshEventArgs e)
{
Functions.DocumentRegister.SetRequiredProperties(_obj);
}
```

## Функции инициализации

Функции инициализации создаются только в редакторах модулей . Используются для:

- заполнения справочников значениями по умолчанию;
- создания сущностей, для которых в редакторе типа сущности установлен флажок Создавать только программно ;
- выдачи пользователям прав доступа на вычисляемые папки модуля ;
- создания предопределенных групп пользователей и ролей.

Пример. Выдача прав роли «Регистраторы исходящих документов»

Предположим, что при инициализации Directum RX регистраторам исходящих документов нужно выдавать права на их регистрацию. Для этого в модуле RecordManagement (Делопроизводство) создается функция инициализации:

```csharp
/// <summary>
/// Выдать права роли "Регистраторы исходящих документов".
/// </summary>
public static void GrantRightsToRegistrationOutgoingRole()
{
var registrationRole = Roles.GetAll().FirstOrDefault(r => r.Sid == Docflow.Constants.Module.RoleGuid.RegistrationOutgoingDocument);
if (registrationRole == null)
return;

// Права на документы.
RecordManagement.OutgoingLetters.AccessRights.Grant(registrationRole, Docflow.Constants.Module.DefaultAccessRightsTypeSid.Register);
RecordManagement.OutgoingLetters.AccessRights.Save();
}
```

## Функции вычисляемых выражений

Функции используются для преобразования данных при настройке вычисляемых выражений . Например, можно создать функцию, которая указывает ФИО сотрудника в винительном падеже или сдвигает текущую дату на количество рабочих дней.

Для создания функций вычисляемых выражений используется атрибут ExpressionElement .

Пример. Получение регистратора документа

Предположим, что для отправки документа на регистрацию в рамках согласования нужно определить регистратора. Для этого в типе документа OfficialDocument (Официальный документ) создается серверная функция:

```csharp
/// <summary>
/// Получить регистратора документа.
/// </summary>
/// <param name="document">Документ.</param>
/// <returns>Регистратор.</returns>
[ExpressionElement("DocumentRegistrar", "DocumentRegistrarDescription")]
public static IEmployee GetDocumentRegistrar(IOfficialDocument document)
{
return Functions.Module.GetRegistrar(document);
}
```

## Функции интеграции

Функции используются для обращения к сервису интеграции . Например, можно создать функцию, чтобы получить список контрагентов из внешней системы.

Для создания функций интеграции используются атрибуты [Public(WebApiRequestType = RequestType.Get)] и [Public(WebApiRequestType = RequestType.Post)] .

Пример. Получение организации

Предположим, что перед отправкой документа контрагенту в сервис обмена нужно проверить его работу. Для этого в модуле Shell (Общие сервисы) создается серверная функция:

```csharp
/// <summary>
/// Проверить работу сервиса обмена.
/// </summary>
[Public(WebApiRequestType = RequestType.Post)]
public virtual void CheckExchange()
{
var result = new StringBuilder(string.Empty);

var boxes = Sungero.ExchangeCore.BusinessUnitBoxes.GetAll()
.Where(b => b.Status == Sungero.ExchangeCore.BusinessUnitBox.Status.Active).ToList();
foreach (var box in boxes)
{
var connectionError = Sungero.ExchangeCore.PublicFunctions.BusinessUnitBox.Remote.CheckConnection(box);

if (!string.IsNullOrEmpty(connectionError))
{
result.AppendLine(string.Format("Check: при проверке а/я НОР ИД {0} возникла ошибка: {1}.", box.Name, connectionError));
}
}

if (result.Length > 0)
throw AppliedCodeException.Create(result.ToString());
}
```

## Функции контрола состояния

Функции используются для добавления контрола состояния на вкладку карточки сущности. Например, можно создать функцию, чтобы в карточке поручения отображалась информация об этапах работы с поручением.

Пример. Информация о процессе ознакомления

Предположим, что нужно создать краткую сводку по документу, чтобы из задания быстро ознакомиться с информацией о нем. Для этого в модуле Docflow (Документооборот) создается серверная функция:

```csharp
/// <summary>
/// Построить сводку по документу.
/// </summary>
/// <returns>Сводка по документу.</returns>
[Remote(IsPure = true)]
public StateView GetDocumentSummary()
{
var documentSummary = StateView.Create();
var documentBlock = documentSummary.AddBlock();

// Содержание.
var subject = !string.IsNullOrEmpty(_obj.Subject) ? _obj.Subject : "-";
documentBlock.AddLabel(string.Format("{0}: {1}", ContractBases.Resources.Subject, subject));
documentBlock.AddLineBreak();

// Сумма договора.
var amount = this.GetTotalAmountDocumentSummary(_obj.TotalAmount);
var amountText = string.Format("{0}: {1}", _obj.Info.Properties.TotalAmount.LocalizedName, amount);
documentBlock.AddLabel(amountText);
documentBlock.AddLineBreak();

return documentSummary;
}
```

## Функции валидации для правил ввода

Функции используются при добавлении правила ввода для настройки диалогов . Например, можно создать функцию, чтобы проверять введенное пользователем значение, отображать сообщение об ошибке, а также добавить собственную прикладную логику.

Пример. Проверка ИНН

Предположим, что в диалоге пользователь вводит ИНН. Нужно проверить корректность введенного значения по контрольной сумме и отобразить текстовое сообщение в случае ошибки. Для этого в модуле Parties (Контрагенты) создается клиентская функция:

```csharp
[LocalizeFunction("Проверка ИНН", "Проверяет корректность ИНН (10 или 12 цифр) по контрольной сумме")]
public void ValidateINN(Sungero.Domain.Client.InputRuleValidationEventArgs e)
{
//Валидации ИНН организации(10 цифр)
var inn = e.Value.Trim();
if(inn.Length == 10)
{
int[] coefficients10 = { 2, 4, 10, 3, 5, 9, 4, 6, 8 };
int checksum10 = 0;

for (int i = 0; i < 9; i++)
checksum10 += coefficients10[i] * int.Parse(inn[i].ToString());
int controlNumber = checksum10 % 11;
if (controlNumber > 9)
controlNumber %= 10;

if (controlNumber != int.Parse(inn[9].ToString()))
e.AddError("Некорректный ИНН. Проверьте правильность ввода");
}

//Валидации ИНН ИП и ФЛ (12 цифр)
if(inn.Length == 12)
{
int[] coefficients11 = { 7, 2, 4, 10, 3, 5, 9, 4, 6, 8 };
int[] coefficients12 = { 3, 7, 2, 4, 10, 3, 5, 9, 4, 6, 8 };

int checksum11 = 0;
for (int i = 0; i < 10; i++)
checksum11 += coefficients11[i] * int.Parse(inn[i].ToString());
int controlNumber11 = checksum11 % 11;
if (controlNumber11 > 9)
controlNumber11 %= 10;

int checksum12 = 0;
for (int i = 0; i < 11; i++)
checksum12 += coefficients12[i] * int.Parse(inn[i].ToString());
int controlNumber12 = checksum12 % 11;
if (controlNumber12 > 9)
controlNumber12 %= 10;

if (controlNumber11 != int.Parse(inn[10].ToString()) || controlNumber12 != int.Parse(inn[11].ToString()))
e.AddError("Некорректный ИНН. Проверьте правильность ввода");
}
}
```

**См. также**

Атрибуты Модификаторы Создание функций модуля Создание функций типов сущностей Наследование элементов разработки
