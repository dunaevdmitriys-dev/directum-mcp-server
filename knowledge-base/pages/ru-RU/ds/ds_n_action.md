---
id: ds_n_action
module: ds
role: Developer
topic: action – описание действия
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Настройка задач и заданий в файле SungeroAdapter.config"
description: "Действие, которое выполняется при отправке задачи или при выполнении задания."
source: webhelp/WebClient/ru-RU/ds_n_action.htm
---

# action – описание действия

Действие, которое выполняется при отправке задачи или при выполнении задания.

Содержит атрибуты: Name , если отсутствует, то Type . Поддерживаются типы действия:

- Abort – прекращение задачи, задания, уведомления;
- Sign – подписание документов из вложений задачи, задания, уведомления простой подписью;
- StartTask – старт задачи, задания, уведомления;
- SuppressPerform – препятствие выполнению прикладного кода варианта выполнения;
- AddApprover – добавление согласующего;
- SetLifeCycle – установка стадии жизненного цикла документа;
- StartDeadlineExtensionTask – запрос на продление срока.

Имена свойств сущностей могут иметь префиксы:

- префикс «!» применяется для обозначения несуществующего свойства, которое может использоваться для передачи значения из блока запроса ask в блок действия action ;
- префикс «@» применяется для обозначения дополнительных свойств, когда не может быть использовано основное свойство. Так, вместо нестабильного свойства ActiveText задачи рекомендуется использовать свойство @ActiveText .

## Abort

Прекращение задачи, задания, уведомления . Выполнение действия препятствует вызову прикладной обработки варианта выполнения.

target . Целевая задача, задание, уведомление . Сейчас поддерживается только значение Task .

Пример

```csharp
<action name="Abort" target="Task" />
```

Действие прекращения текущей задачи.

## Sign

Подписание документов из вложений простой подписью. Используется только для task .

target . Целевая группа вложений.

type . Тип подписи.

Пример

```csharp
<action name="Sign" target="DocumentGroup" type="Approval">
```

Действие по подписанию группы документов утверждающей подписью.

## StartTask

Старт задачи или уведомления.

interface . Тип задачи.

add . Свойства добавляемые.

set . Свойства устанавливаемые.

Свойства add и set содержат одинаковые атрибуты:

- source . Путь к источнику устанавливаемого значения вида "Job.xxx" или "Task.xxx";
- destination . Путь к получателю устанавливаемого значения вида "Job.xxx" или "Task.xxx".

Пример

```csharp
<ask type="Date" display="Sungero.RecordManagement.Shared.DeadlineExtensionTask.DeadlineExtensionTaskSystem.Property_NewDeadline" target="!NewDeadline" onlyFuture="true" required="true" />
<ask type="Text" display="Sungero.RecordManagement.Shared.DeadlineExtensionTask.DeadlineExtensionTaskSystem.Property_Reason" target="!Reason" required="true" />
<action name="StartTask" interface="Sungero.RecordManagement.IDeadlineExtensionTask">
<set source="Job.!NewDeadline" destination="Task.NewDeadline" />
<set source="Job.!Reason" destination="Task.@ActiveText" />
</action>
```

Действие по запросу продления срока с указанием нового срока и причины запроса.

## SuppressPerform

Препятствует выполнению прикладного кода варианта выполнения.

Пример

```csharp
<action name="SuppressPerform">
```

## AddApprover

Добавление согласующего.

approver . Согласующий.

deadline . Срок.

Пример

```csharp
<action name="AddApprover" approver="!Approver" />
```

Действие по добавлению исполнителя в задание согласования из временной переменной.

## SetLifeCycle

Установка стадии жизненного цикла документа.

target . Целевая группа вложений.

state . Стадия жизненного цикла документа.

Пример

```csharp
<action name="SetLifeCycle" target="DocumentGroup" state="Obsolete" />
```

Действие по установке жизненного цикла группы документов в состояние «Устаревший».

## StartDeadlineExtensionTask

Запрос на продление срока. Задается аналогично StartTask .

Пример

```csharp
<ask type="Date" display="Sungero.RecordManagement.Shared.DeadlineExtensionTask.DeadlineExtensionTaskSystem.Property_NewDeadline" target="!NewDeadline" onlyFuture="true" required="true" />
<ask type="Text" display="Sungero.RecordManagement.Shared.DeadlineExtensionTask.DeadlineExtensionTaskSystem.Property_Reason" target="!Reason" required="true" />
<action name="StartDeadlineExtensionTask">
<set source="Job.!NewDeadline" destination="Task.NewDeadline" />
<set source="Job.!Reason" destination="Task.@ActiveText" />
</action>
```

Действие по запросу продления срока с указанием нового срока и причины запроса.
