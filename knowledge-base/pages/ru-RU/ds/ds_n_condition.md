---
id: ds_n_condition
module: ds
role: Developer
topic: condition – условия, наложенные на действие
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Настройка задач и заданий в файле SungeroAdapter.config"
description: "Действие выполняется только при удовлетворении всех наложенных условий."
source: webhelp/WebClient/ru-RU/ds_n_condition.htm
---

# condition – условия, наложенные на действие

Действие выполняется только при удовлетворении всех наложенных условий.

Содержит атрибут Name . Если он отсутствует, то Type . Возможные типы условия:

- And – условие «И» над группой условий;
- Or – условие «ИЛИ» над группой условий;
- Not – условие «НЕ» над условием;
- Equal – условие равенства;
- BlockEqual – условие равенства в свойствах блока, который используется в схеме бизнес-процесса ;
- IsNull – условие равенства «null»;
- ClientHasFeature – условие наличия у мобильного приложения заданной возможности;
- ClientHasCertificates – условие наличия у пользователя сертификатов, зарегистрированных в системе Directum RX;
- RuleHasCondition – условие наличия у правила согласования заданного условия;
- DocumentIsType – условие наличия среди документов заданной группы хотя бы одного документа заданного типа;
- DocumentHasExchangeServices – условие, проверяющее, что документ можно отправить через сервис электронного обмена;
- AuthorIsApprover – условие, проверяющее, что автор задания является одним из согласующих;
- AllowAdditionalApprovers – условие наличия разрешения добавлять дополнительных согласующих;
- NotCollapsed – условие, проверяющее, что задание не схлопнуто с другим заданием;
- RuleHasStage – условие наличия этапа в правиле согласования;
- IsPerformBySubstitution – возможность выполнения задания за замещаемого сотрудника;

• IsManagerOfPerformer – условие, проверяющее, что сотрудника замещает его руководитель. Рекомендуется использовать совместно с другими условиями;

- AllowAcquaintanceBySubstitution – возможность ознакомления с документами за замещаемого сотрудника;
- EmployeeInGroup – условие, проверяющее, что текущий сотрудник добавлен в указанную группу ;
- IsManagerOfPerformerCondition – условие, проверяющее, что подписывающий является руководителем замещаемого сотрудника;
- ClientHasOnlyCloudSignCertificates – условие, проверяющее, что все используемые сотрудником сертификаты привязаны к плагинам облачного подписания.

Пример

```csharp
<condition name="DocumentIsType" target="DocumentGroup" value="IIncomingDocumentBase" />
```

Условие по типу документа (описание см. ниже).

## And

Условие «И» над группой условий.

condition . Группа условий.

Пример

```csharp
<condition name="And">
<condition name="DocumentIsType" target="DocumentGroup" value="IIncomingDocumentBase" />
<condition name="DocumentIsType" target="DocumentGroup" value="IOutgoingDocumentBase" />
</condition>
```

Условие «И» над вложенными условиями.

## Or

Условие «ИЛИ» над группой условий.

condition . Группа условий.

Пример

```csharp
<condition name="Or">
<condition name="DocumentIsType" target="DocumentGroup" value="IIncomingDocumentBase" />
<condition name="DocumentIsType" target="DocumentGroup" value="IOutgoingDocumentBase" />
</condition>
```

Условие «ИЛИ» над вложенными условиями.

## Not

Условие «НЕ» над условием.

condition . Условие.

Пример

```csharp
<condition name="Not">
<condition name="DocumentIsType" target="DocumentGroup" value="IIncomingDocumentBase" />
</condition>
```

Условие «НЕ» над вложенным условием.

## Equal

Условие равенства.

target . Проверяемый объект – свойство делового процесса Work . Например, IsConfirmSigning , Stage.NeedStrongSign .

value . Проверяемое значение.

Пример

```csharp
<condition name="Equal" target="IsConfirmSigning" value="true" />
```

Условие равенства требования согласуемой подписи истине.

## BlockEqual

Условие равенства в свойствах блока.

target . Проверяемый объект – блок прикладного модуля.

value . Проверяемое значение.

Пример

```csharp
<condition name="BlockEqual" target="AllowChangeReworkPerformer" value="true" />
```

Условие, разрешающее выбрать ответственного за доработку.

## IsNull

Условие равенства null.

target . Проверяемый объект – свойство делового процесса Work . Например, IsConfirmSigning , Stage.NeedStrongSign .

Пример

```csharp
<condition name="IsNull" target="ExchangeService" />
```

Условие отсутствия сервиса обмена.

## ClientHasFeature

Условие наличия у мобильного приложения пользователя заданной возможности.

value . Наименование функциональной возможности.

Пример

```csharp
<condition name="ClientHasFeature" value="DocumentSignSungero" />
```

Условие наличия функциональной возможности подписания документов в Sungero.

## ClientHasCertificates

Условие наличия у пользователя сертификатов, зарегистрированных в системе Directum RX.

Пример

```csharp
<condition name="ClientHasSertificates" />
```

Условие наличия у пользователя сертификатов.

## RuleHasCondition

Условие наличия у правила согласования заданного условия.

condition . Условие правила согласования.

Пример :

```csharp
<condition name="RuleHasCondition" condition="DeliveryMethod" />
```

У правила согласования задан способ доставки.

## DocumentIsType

Условие наличия среди документов заданной группы хотя бы одного документа заданного типа.

target . Целевая группа вложений.

value . Тип документа.

Пример

```csharp
<condition name="DocumentIsType" target="DocumentGroup" value="IIncomingDocumentBase" />
```

Условие наличия входящего документа.

## DocumentHasExchangeServices

Условие, проверяющее, что у документ можно отправить через сервис электронного обмена.

Пример

```csharp
<condition name="DocumentHasExchangeServices"/>
```

## AuthorIsApprover

Условие, проверяющее, что автор задания является одним из согласующих.

needStrongSign . Признак требования усиленной подписи. По умолчанию: false .

Пример

```csharp
<condition name="AuthorIsApprover" />
```

Условие того, что автор является одним из согласующих.

## AllowAdditionalApprovers

Условие наличия разрешения добавлять дополнительных согласующих.

Пример

```csharp
<condition name="AllowAdditionalApprovers" />
```

Дополнительные согласующие разрешены.

## NotCollapsed

Условие, проверяющее, что задание не схлопнуто с другим заданием.

stage . Имя этапа, проверка на схлопываемость с которым проверяется. Например, Sending .

target . Имя целевого свойства задания, в котором находится информация о схлопываемых заданиях. Например, CollapsedStagesTypesSig .

Пример

```csharp
<condition name="NotCollapsed" stage="Sending" target="CollapsedStagesTypesSig" />
```

Условие того, что задание отправки не схлопнуто с текущим.

## RuleHasStage

Условие наличия этапа в правиле согласования.

stage . Имя этапа. Например, Sending .

Пример

```csharp
<condition name="RuleHasStage" stage="Sign"/>
```

Условие того, что правило имеет этап подписания.

## IsPerformBySubstitutionCondition

Возможность выполнения задания за замещаемого сотрудника.

Пример

```csharp
<condition name="IsPerformBySubstitution" />
```

## IsManagerOfPerformer

Условие, проверяющее, что сотрудника замещает его руководитель. Рекомендуется использовать совместно с другими условиями, например IsPerformBySubstitution.

Пример

```csharp
<condition name="Or">
<condition name="Not">
<condition name="IsPerformBySubstitution" />
</condition>
<condition name="IsManagerOfPerformer" />
</condition>
```

Условие, что за сотрудника нельзя выполнять задания по замещению или его замещает руководитель.

## AllowAcquaintanceBySubstitution

Возможность ознакомления с документами за замещаемого сотрудника.

Пример

```csharp
<condition name="AllowAcquaintanceBySubstitution" />
```

Разрешено ознакомиться с документами за замещаемого сотрудника.

## EmployeeInGroup

Условие, проверяющее, что текущий сотрудник добавлен в указанную группу.

value . Идентификатор группы, в которую добавлен сотрудник. Можно указать ИД из карточки группы в Directum RX или ее Sid .

Пример

```csharp
<condition name="EmployeeInGroup" value="10000" />
```

Условие, проверяющее, что сотрудник включен в группу с указанным ИД.

## IsManagerOfPerformerCondition

Условие, проверяющее, что подписывающий является руководителем замещаемого сотрудника.

Пример

```csharp
<condition name="IsManagerOfPerformerCondition" />
```

Разрешено подписывать документ, если пользователь является руководителем замещаемого.

## ClientHasOnlyCloudSignCertificates

Условие, проверяющее, что все используемые сотрудником сертификаты привязаны к плагинам облачного подписания.

Пример

```csharp
<condition name="ClientHasOnlyCloudSignCertificates" />
```

Разрешено подписывать документ, если все сертификаты пользователя привязаны к плагинам облачного подписания.
