---
id: ds_n_task
module: ds
role: Developer
topic: task – описание задачи
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Настройка задач и заданий в файле SungeroAdapter.config"
description: "Содержит следующие атрибуты и вложенные элементы: type. Тип задачи; allowStart. Признак, определяющий возможность старта задачи. По умолчанию: false; documentActionName. Наимено"
source: webhelp/WebClient/ru-RU/ds_n_task.htm
---

# task – описание задачи

Содержит следующие атрибуты и вложенные элементы:

- type . Тип задачи;
- allowStart . Признак, определяющий возможность старта задачи. По умолчанию: false ;
- documentActionName . Наименование действия для отправки документа в задаче (см. руководство разработчика Directum RX, раздел «Действия» Sungero.Metadata.ActionMetadata.Name). Например, SendForApproval ;
- viewMode . Режим отображения делового процесса ;
- flags . Флаги задачи, задания, уведомления . По умолчанию: 0 ;
- action . Действия , выполняемые при отправке задачи;
- autoRightsAttachmentGroup . Группа вложений , на документы из которой автоматически назначаются права при старте задачи;
- ask . Запрашиваемые у пользователя параметры.

Пример

```csharp
<!-- Свободное согласование. -->
<task type="Sungero.Docflow.IFreeApprovalTask" allowStart="true" documentActionName="SendForFreeApproval">
<ask type="Document" target="ForApprovalGroup.ElectronicDocuments" required="true" collection="false" onCreateTaskSetFromAttachment="true"
display="Sungero.Docflow.Shared.FreeApprovalTask.FreeApprovalTaskSystem.AttachmentGroup_ForApprovalGroupTitle"/>
<ask type="Recipient" target="Approvers" required="true" validateOnServer="true" />
<ask type="Date" target="MaxDeadline" onlyFuture="true" />
<ask type="Pick" target="ReceiveOnCompletion" required="true" />
<ask type="Bool" target="ReceiveNotice" />
</task>
```

Здесь задается задача с разрешенным стартом и действием для документа SendForFreeApproval . Указан ряд запрашиваемых параметров (см. описания ниже).
