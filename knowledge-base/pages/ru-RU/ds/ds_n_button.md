---
id: ds_n_button
module: ds
role: Developer
topic: button – кнопка диалога
breadcrumb: "Разработка > Особенности разработки для мобильных решений > Настройка задач и заданий в файле SungeroAdapter.config"
description: "type. Тип стандартной кнопки. Возможные значения: Ok - ОК; Cancel - Отмена; Yes - Да; YesToAll - Да для всех; No - Нет; Abort - Прервать; Retry - Повторить; Ignore – Игнори"
source: webhelp/WebClient/ru-RU/ds_n_button.htm
---

# button – кнопка диалога

type . Тип стандартной кнопки. Возможные значения:

- Ok - ОК;
- Cancel - Отмена;
- Yes - Да;
- YesToAll - Да для всех;
- No - Нет;
- Abort - Прервать;
- Retry - Повторить;
- Ignore – Игнорировать .

name . Имя для нестандартной кнопки. Может указываться:

- имя строки локализации, например Sungero.Docflow.Resources.CreateActionItem;
- имя строки локализации внешней сборки в формате <путь к файлу сборки>$<полное имя класса ресурсов>.<имя ресурса>, например C:\Temp\MyResources.dll$MyResources.Resources.MyText;
- не локализуемая строка (если начинается с "!").

isResult . Признак того, что эта кнопка выбирается автоматически. По умолчанию false .

Пример :

```csharp
<button type="Yes" isResult="true"/>
<button name="Sungero.Docflow.Resources.CreateActionItem"/>
```

Стандартная кнопка, отмеченная как результат и нестандартная кнопка.
