---
id: ds_n_ask
module: ds
role: Developer
topic: ask – запрашиваемые параметры
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Настройка задач и заданий в файле SungeroAdapter.config"
description: "Содержит атрибуты: Name, если отсутствует, то Type. Возможные типы параметра:"
source: webhelp/WebClient/ru-RU/ds_n_ask.htm
---

# ask – запрашиваемые параметры

Содержит атрибуты: Name , если отсутствует, то Type . Возможные типы параметра:

- Bool – логическое значение;
- Date – дата;
- Document – документ;
- Pick – перечисление;
- Recipient – базовый субъект авторизации;
- ReferenceRecord – запись справочника;
- Text – текст;
- ApprovalRule – правило согласования;

• ProcessKind – вариант процесса;

- Employee – сотрудник;
- OfficialDocument – официальный документ;
- Signatory – подписант;
- User – пользователь.

required . Признак, определяющий, является ли запрашиваемый параметр обязательным для заполнения. По умолчанию false .

readonly . Признак, определяющий, что параметр доступен только для чтения. По умолчанию false .

validateOnServer . Признак, определяющий необходимость отправки параметра на сервис сразу же после изменения его значения. По умолчанию false .

target . Целевое свойство. По умолчанию !local .

display . Отображаемое имя запрашиваемого параметра. Например, «Sungero.Docflow.Shared.IncomingDocumentBase.IncomingDocumentBase.MarkDocumentAsObsolete».

collection . Признак, определяющий, является ли параметр коллекцией. По умолчанию false .

condition . Условия отображения параметра на клиенте. Параметр отображается только при удовлетворении всех условий.

if . Действия, выполняемые при совпадении указанного значения параметра с заданным.

uiFilteringDisabled . Признак включенного ограничения видимости оргструктуры. Значение зависит от настройки видимости в Directum RX:

- false – ограничение включено. Для запрашиваемых параметров применяется фильтрация по текущей организации;
- true – ограничение выключено.

По умолчанию фильтрация отключена для Signatory , см. пример . Эту настройку можно использовать, чтобы отключить фильтрацию для типов параметров Recipient , ReferenceRecord , Employee , User и DeadlineExtensionEmployee .

Пример

```csharp
<ask type="Bool" display="Sungero.Docflow.Shared.IncomingDocumentBase.IncomingDocumentBase.MarkDocumentAsObsolete" default="true">
<if value="true">
<action name="SetLifeCycle" target="DocumentGroup" state="Obsolete" />
</if>
<condition name="Or">
<condition name="DocumentIsType" target="DocumentGroup" value="IIncomingDocumentBase" />
<condition name="DocumentIsType" target="DocumentGroup" value="IOutgoingDocumentBase" />
</condition>
</ask>
```

Запрашиваемый параметр необходимости отметки документа устаревшим с действием при значении true и условиями для отображения.

## Bool

Логическое значение.

default . Значение свойства по умолчанию. По умолчанию пусто.

Пример

```csharp
<ask type="Bool" display="Sungero.Docflow.Shared.OfficialDocument.OfficialDocument.MarkDocumentAsObsolete" default="true">
```

Запрашиваемый параметр отметки документа как устаревшего со значением по умолчанию true .

## Date

Дата.

onlyFuture . Признак, показывающий, что дата должна быть в будущем. По умолчанию false .

Пример

```csharp
<ask type="Date" target="MaxDeadline" onlyFuture="true" />
```

Запрашиваемый параметр срока с ограничением на значение только в будущем .

## Document

Документ.

onCreateTaskSetFromAttachment . Признак, показывающий, что при создании задачи необходимо установить значение параметра из вложений. По умолчанию false .

Пример

```csharp
<ask type="Document" target="ForApprovalGroup.ElectronicDocuments" required="true" onCreateTaskSetFromAttachment="true"
display="Sungero.Docflow.Shared.FreeApprovalTask.FreeApprovalTaskSystem.AttachmentGroup_ForApprovalGroupTitle"/>
```

Запрашиваемый обязательный параметр документа с установленным отображаемым именем и признаком того, что при создании задачи значение будет установлено из вложений.

## Pick

Перечисление.

target . При вычислении целевого свойства к target добавляется постфикс «AllowedItems».

Пример

```csharp
<ask type="Pick" target="Mark" required="true" />
```

Запрашиваемый обязательный параметр отметки из перечисления.

## Recipient

Базовый субъект авторизации.

Пример

```csharp
<ask type="Recipient" target="ReqApprovers" collection="true" readonly="true"/>
```

Запрашиваемый обязательный параметр обязательных согласующих в виде коллекции.

## ReferenceRecord

Запись справочника.

exclude . Коллекция имен записей справочников, которые необходимо исключить из выборки для реквизита. Содержит единственный атрибут name - имя исключаемой записи.

Пример

```csharp
<ask type="ReferenceRecord" target="ExchangeService" required="true">
```

Запрашиваемый обязательный параметр сервиса обмена из справочника.

## Text

Текст.

Пример

```csharp
<ask type="Text" target="ActiveText" display="Sungero.Docflow.Shared.ApprovalTask.ApprovalTaskSystem.Property_AbortingReason" required="true" />
```

Запрашиваемый обязательный параметр текста с установленным отображаемым именем.

## ApprovalRule

Правило согласования.

Пример

```csharp
<ask type="ApprovalRule" target="ApprovalRule" required="true" validateOnServer="true"/>
```

Запрашиваемый обязательный параметр правило согласования с отправкой на сервис при изменении.

## ProcessKind

Вариант процесса.

Пример

```csharp
<ask type="ProcessKind" target="ProcessKind" required="true" validateOnServer="true" />
```

Запрашиваемый обязательный параметр вариант процесса.

## Employee

Сотрудник.

include . Коллекция фильтров для записей справочника Сотрудники , которые необходимо включить в выборку для запрашиваемого параметра. Содержит дочерние узлы item с обязательным атрибутом name – названием фильтрации для выборки. Возможные значения атрибута:

- CurrentEmployee – текущий сотрудник;
- SubstitutedEmployees – замещаемые сотрудники. Содержит необязательный атрибут onlyDirect – признак проверки замещений. Возможные значения:

• true – в выборку попадают только замещения сотрудника, добавленные в справочник Замещения;

• false – в выборку попадают все замещения: из справочника Замещения, а также системные.

- Если атрибут onlyDirect не добавлен, в выборку попадают все замещения;
- Subordinates – подчиненные сотрудники.

Примечание. Если в блок include не добавлено ни одного узла item , то в выборку не попадает ни одна запись справочника.

Пример

```csharp
<ask type="Employee" target="Employee" required="true" validateOnServer="true">
<include>
<item name="CurrentEmployee" />
<item name="SubstitutedEmployees" onlyDirect="false"/>
</include>
</ask>
```

Запрашивается обязательный параметр в виде коллекции записей справочников, в которую включены текущий сотрудник, замещаемые им, а также подчиненные сотрудники.

## OfficialDocument

Официальный документ, см. ask Document .

## Signatory

Подписант.

Пример

```csharp
<ask type="Signatory" target="Signatory" required="true" uiFilteringDisabled="true" validateOnServer="true">
<condition name="RuleHasStage" stage="Sign" />
</ask>
```

Отключение фильтрации для запроса подписанта при включенном ограничении видимости оргструктуры.

## User

Пользователь.

Пример

```csharp
<ask type="User" target="Addressee" readonly="true">
```

Запрашиваемый неизменяемый параметр адресата.
