---
id: ds_otklyuchenie_sobytiy
module: ds
role: Developer
topic: Отключение событий сущностей и свойств
breadcrumb: "Разработка > Программный код > События типов сущностей"
description: "События типов сущностей и свойств сущностей можно программно отключить. Это позволяет оптимизировать вычисления при обработке большого количества объектов. К примеру, можно..."
source: webhelp/WebClient/ru-RU/ds_otklyuchenie_sobytiy.htm
---

# Отключение событий сущностей и свойств

События типов сущностей и свойств сущностей можно программно отключить. Это позволяет оптимизировать вычисления при обработке большого количества объектов. К примеру, можно массово заполнить поля в документах и при этом отключить автозаполнение связанных свойств.

Какие события можно отключать | Какие события нельзя отключать
• создание типа сущности ( Created ); • cохранение типа сущности ( BeforeSave , Saving , Saved , AfterSave ); • удаление типа сущности ( BeforeDelete , Deleting , AfterDelete ); • до подписания ( BeforeSigning ); • до сохранения истории ( BeforeSaveHistory ); • изменение значения свойства ( Changed ); • изменение, добавление и удаление свойств дочерней коллекции ( Changed , Added , Deleted ) | • показ, закрытие и обновление формы ( Showing , Closing , Refresh ); • изменение значения контрола ( ValueInput ); • серверные события типа задачи ( BeforeStart , BeforeRestart , BeforeResume , BeforeAbort , AfterSuspend ); • до выполнения задания ( BeforeComplete ); • фильтрация всех типов сущностей ( Filtering ); • UI-фильтрация справочника ( UIFiltering ); • смена типа документа ( ConvertingFrom )

Для отключения событий типов сущностей используется метод EntityEvents.Disable() . Например, чтобы создать документ без выполнения событий «До сохранения» и «После сохранения», укажите их при вызове метода:

```csharp
public static void CreateDocNoEvents(Sungero.Domain.Client.ExecuteActionArgs e)
{
using (EntityEvents.Disable(AgendaDocuments.Info.Events.BeforeSave, AgendaDocuments.Info.Events.AfterSave))
{
var doc = AgendaDocuments.Create();
doc.Name = "Протокол совещания" + Calendar.Now;
doc.Save();
}
}
```

С помощью метода EntityEvents.DisableAll() можно отключить сразу все события сущности:

```csharp
using (EntityEvents.DisableAll(AgendaDocuments.Info))
{
_obj.Author = Users.Get(1);
}
```

События свойств сущности отключаются аналогично. Далее в первом примере кода отключается событие «Изменение значения свойства» свойства Name , во втором –события всех свойств сущности AgendaDocument :

```csharp
using (EntityEvents.Disable(AgendaDocuments.Info.Properties.Name.Events.Changed))
using (EntityEvents.DisableAll(AgendaDocuments.Info.Properties))
```
