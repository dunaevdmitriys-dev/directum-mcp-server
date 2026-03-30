---
id: ds_modifikatory
module: ds
role: Developer
topic: Модификаторы
breadcrumb: "Разработка > Программный код > Функции"
description: "Как работать с модификаторами. Virtual. Override. Static"
source: webhelp/WebClient/ru-RU/ds_modifikatory.htm
---

# Модификаторы

Модификаторы используются для управления доступом, наследованием и способом выполнения функций.

При объявлении функций можно использовать следующие модификаторы:

- public . Указывается, чтобы задать к функции общий доступ из любого другого кода с максимальными правами без ограничений. Модификатор автоматически добавляется в определении функции при ее создании ;
- virtual . Указывается, чтобы разрешить переопределять функции в наследниках или перекрывать их.
- Пример объявления:

```csharp
public virtual void FullName();
```

- override . Указывается при переопределении функции базового типа сущности. Переопределять можно наследуемые функции, для которых в базовом типе сущности указан модификатор virtual .

Пример объявления:

```csharp
public override void FullName();
```

- Подробнее см. пример переопределения функции базового типа документа OfficialDocument .
- Примечание. Функции перекрытых модулей с указанным модификатором virtual , также можно переопределять с помощью модификатора override .
- static . Указывается, когда при вызове функции типа сущности не нужно создавать экземпляр конкретной сущности. При этом параметр _obj становится недоступным.
- важно. Для функций модуля не обязательно указывать модификатор, так как они считаются статическими, даже если модификатор static не указан.
- Пример объявления:

```csharp
public static IContractualDocument GetContractualDocumentIgnoreAccessRights(long documentId);
```

Пример 1. Создание функции в типе сущности OfficialDocument (Официальный документ)

```csharp
/// <summary>
/// Получить задания на возврат по документу.
/// </summary>
/// <param name="returnTask">Задача.</param>
/// <returns>Задания на возврат.</returns>
[Remote(IsPure = true)]
public static List<Sungero.Workflow.IAssignment> GetReturnAssignments(Sungero.Workflow.ITask returnTask)
{
return GetReturnAssignments(new List<Sungero.Workflow.ITask>() { returnTask });
}
```

Пример 2. Создание функции в модуле RecordManagementUI (Делопроизводство)

```csharp
/// <summary>
/// Серверная функция для создания поручения по документу.
/// </summary>
/// <param name="document">Документ на рассмотрение.</param>
/// <returns>Поручение по документу.</returns>
[Remote(PackResultEntityEagerly = true), Public]
public virtual IActionItemExecutionTask CreateActionItemExecution(IOfficialDocument document)
{
return CreateActionItemExecution(document, Assignments.Null);
}
```

**См. также**

Создание функций модуля Создание функций типов сущностей Атрибуты
