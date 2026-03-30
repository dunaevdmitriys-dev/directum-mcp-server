---
id: ds_n_taskdialog
module: ds
role: Developer
topic: taskDialog – диалог с выбором варианта для автоматической обработки
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Настройка задач и заданий в файле SungeroAdapter.config"
description: "type. Тип диалога. Возможные значения: Information – информация; Question – вопрос; Warning – предупреждение; Error – ошибка. text. Текст диалога. Может указываться: имя стро"
source: webhelp/WebClient/ru-RU/ds_n_taskdialog.htm
---

# taskDialog – диалог с выбором варианта для автоматической обработки

type . Тип диалога. Возможные значения:

- Information – информация;
- Question – вопрос;
- Warning – предупреждение;
- Error – ошибка.

text . Текст диалога. Может указываться:

- имя строки локализации, например Sungero.Docflow.Resources.ExecuteWithoutCreatingActionItem;
- имя строки локализации внешней сборки в формате <путь к файлу сборки>$<полное имя класса ресурсов>.<имя ресурса>, например C:\Temp\MyResources.dll$MyResources.Resources.MyText;
- не локализуемая строка (если начинается с «!» ).

button . Кнопки диалога .

Пример :

```csharp
<taskDialog type="Question" text="Sungero.Docflow.Resources.ExecuteWithoutCreatingActionItem">
<button type="Yes" isResult="true"/>
<button type="No"/>
<button name="Sungero.Docflow.Resources.CreateActionItem"/>
</taskDialog>
```

Диалог типа вопрос с тремя кнопками.
