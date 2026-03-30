---
id: ds_n_sungeroadapter_config_kak_dobavit_vkladku_rezolucii
module: ds
role: Developer
topic: Как добавить вкладку «Резолюции» в собственное задание
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Примеры доработок в файле SungeroAdapter.config"
description: "В стандартной поставке у задания на рассмотрение документа в Directum Solo отображается три вкладки: «Переписка», «Резолюции» и «Вложения». Сотрудник, получивший такое задание,..."
source: webhelp/WebClient/ru-RU/ds_n_sungeroadapter_config_kak_dobavit_vkladku_rezolucii.htm
---

# Как добавить вкладку «Резолюции» в собственное задание

В стандартной поставке у задания на рассмотрение документа в Directum Solo отображается три вкладки: «Переписка», «Резолюции» и «Вложения». Сотрудник, получивший такое задание, может редактировать проекты поручений на вкладке «Резолюции».

Предположим, что в компании разработано собственное задание на рассмотрение документа, по которому помощник руководителя выносит резолюции. Чтобы вкладка «Резолюции» отображалась в Directum Solo:

1. Убедитесь, что в среде разработки Development Studio для проектов резолюций, создаваемых помощником в рамках нового задания, в свойстве IsDraftResolution (Входит в проект резолюции) указано значение True .

- 2. В конфигурационном файле SungeroAdapter.config в секцию entities скопируйте блок стандартного задания на рассмотрение документа. Пример настройки:

```csharp
<!-- Задание на рассмотрение документа руководителем с проектом поручения от помощника. -->
<job type="Sungero.RecordManagement.IReviewDraftResolutionAssignment" rof="true" viewMode="Review">
<!-- Группы вложений, содержащие резолюции. -->
<resolutionAttachmentGroups>
<attachmentGroup name="ResolutionGroup" />
</resolutionAttachmentGroups>
</job>
```

- 3. В скопированном блоке в параметре type укажите тип вашего задания на рассмотрение.
- 4. В скопированный блок добавьте блок с группой вложений для проектов поручений, например:

```csharp
<!-- Группы вложений, содержащие резолюции. -->
<resolutionAttachmentGroups>
<attachmentGroup name="ResolutionGroup" />
<attachmentGroup name="CustomGroup1" />
<attachmentGroup name="CustomGroup2" />
</resolutionAttachmentGroups>
```

- 3. Настройте варианты выполнения задания . Для вариантов выполнения, в которых требуется отправка поручений в работу, укажите флаги 1 (+2) или 2 (+4), например:

```csharp
<!--Вариант выполнения На исполнение. -->
<result name="ForExecution" flag="4">
<params/>
</result>
```

В результате в Directum Solo в задании будет отображаться вкладка «Резолюции» со вложенными при старте задачи проектами поручения.

Пример. Добавление вкладки «Резолюции» в задание

```csharp
<?xml version="1.0" encoding="utf-8"?>
<configuration>
<entities>

<job type="Sungero.RecordManagement.ICustomDocumentReviewAssignment" rof="true" viewMode="Review">

<!-- Группы вложений, содержащие резолюции. -->
<resolutionAttachmentGroups>
<attachmentGroup name="ResolutionGroup"/>
<attachmentGroup name="CustomGroup1"/>
<attachmentGroup name="CustomGroup2"/>
</resolutionAttachmentGroups>

<!-- Переадресовать рассмотрение. -->
<result name="Forward">
<params>
<param code="Addressee"/>
</params>
</result>

<!-- Принято к сведению. -->
<result name="Informed" isEnabledOffline="true">
<params/>
</result>

<!-- Создать поручение. -->
<result name="CreateActionItem" flag="820" isEnabledOffline="true">
<params/>
</result>

<!-- Утвердить проект резолюции. -->
<result name="DraftResApprove" flag="4" isEnabledOffline="true">
<params/>
</result>

<!-- Вернуть помощнику. -->
<result name="DraftResRework" flag="1" isEnabledOffline="true">
<params/>
</result>
</job>

</entities>
```
