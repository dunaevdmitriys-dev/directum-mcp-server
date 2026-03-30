---
id: ds_n_podav_dialog
module: ds
role: Developer
topic: Подавляемые диалоги
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Поддержка типов заданий в NOMAD"
description: "Диалоги, которые автоматически обрабатываются на сервисе NOMAD и не отображаются в клиентском приложении, т.е. подавляются."
source: webhelp/WebClient/ru-RU/ds_n_podav_dialog.htm
---

# Подавляемые диалоги

Диалоги, которые автоматически обрабатываются на сервисе NOMAD и не отображаются в клиентском приложении, т.е. подавляются.

Диалог с выбором варианта – CreateTaskDialog

```csharp
<job type="Sungero.RecordManagement.IReviewManagerAssignment">
<result name="AddAssignment">
<taskDialog type="Question" text="Sungero.Docflow.Resources.ExecuteWithoutCreatingActionItem">
<button type="Yes" isResult="true"/>
<button type="No"/>
<button name="Sungero.Docflow.Resources.CreateActionItem"/>
</taskDialog>
</result>
</job>
```

Диалог подтверждения – CreateConfirmDialog

По умолчанию все диалоги подтверждения выполняются с результатом «Да».
