---
id: ds_sozd_funkciy_integrazii
module: ds
role: Developer
topic: Создание функций интеграции
breadcrumb: "Разработка > Программный код > Функции"
description: "Интеграционными могут быть только серверные и разделяемые функции модуля. Если функции создавать в типе сущности, то они будут недоступны в сервисе интеграции. В базовом..."
source: webhelp/WebClient/ru-RU/ds_sozd_funkciy_integrazii.htm
---

# Создание функций интеграции

Интеграционными могут быть только серверные и разделяемые функции модуля . Если функции создавать в типе сущности, то они будут недоступны в сервисе интеграции. В базовом решении по умолчанию есть функции для настройки интеграции , к которым можно обратиться через сервис интеграции . На своем слое разработки эти функции можно переопределить . Кроме этого, на слое можно любую серверную или разделяемую функцию базового решения сделать интеграционной или же с нуля написать свою функцию интеграции.

ПРИМЕЧАНИЕ. Функции интеграции необходимо создавать только в модулях. Иначе в сервисе интеграции они будут недоступны.

Чтобы создать функцию интеграции:

- 1. Используйте модификатор public .
- 2. Добавьте атрибут :

• [Public(WebApiRequestType = RequestType.Get)] , если функция получает данные из Directum RX. В HTTP-запросах для обращения к таким функциям используйте метод GET ;

• [Public(WebApiRequestType = RequestType.Post)] , если функция выполняет действия в системе Directum RX. В HTTP-запросах для обращения к таким функциям используйте метод POST .

- ПРИМЕЧАНИЕ. Длина стартовой строки GET-запроса должна быть не больше 2048 символов. Если функция получает данные из Directum RX, и при этом в ней нужно передать длинный параметр, например массив значений, используйте метод POST.
- 3. Проверьте:

• типы данных у параметров и возвращаемого значения функции;

• имя функции. Если в модуле базового решения есть интеграционная функция и у нее нет модификатора virtual , то в перекрытиях этого модуля нельзя добавлять функцию с таким же именем. В лог-файле сервиса интеграции появится ошибка. В разных модулях имена функции могут повторяться. Например, есть Модуль 1 и Модуль 2. Имена функций в них могут быть одинаковые. В перекрытии Модуля 1 имена функций не должны совпадать с именами функций в родительском модуле.

- 4. Опубликуйте разработку .

Примеры интеграционных функций из базового решения Directum RX:

```csharp
/// <summary>
/// Получить вид документа в шаблонах по guid.
/// </summary>
/// <param name="documentType">Тип документа.</param>
/// <param name="kindGuid">Guid вида документа, заданный при инициализации.</param>
/// <returns>ИД вида документа.</returns>
/// <remarks>Виды документов ищутся по связке (guid экземпляра, id записи) в ExternalLink.</remarks>
[Public(WebApiRequestType = RequestType.Get)]
public virtual long GetDocumentKindIdByGuid(Guid documentType, Guid kindGuid)
{
// GUID для значения "<Любые документы>" у свойства Тип документа в шаблонах.
var allDocumentTypeGuid = Guid.Parse(Sungero.Docflow.PublicConstants.DocumentTemplate.AllDocumentTypeGuid);
var documentKind = Sungero.Docflow.PublicFunctions.DocumentKind.Remote.GetNativeDocumentKindRemote(kindGuid);
var typeGuid = Guid.Parse(documentKind.DocumentType.DocumentTypeGuid);
if (documentKind != null && documentKind.Status == Sungero.Docflow.DocumentKind.Status.Active &&
(documentType == allDocumentTypeGuid || documentType == typeGuid))
{
return documentKind.Id;
}
return 0;
}

/// <summary>
/// Сформировать пакет бинарных образов документов на основе пакета документов из DCS.
/// </summary>
/// <param name="dcsPackage">Пакет документов из DCS.</param>
[Public(WebApiRequestType = RequestType.Post)]
public virtual void PrepareBlobPackage(Structures.Module.IDcsPackage dcsPackage)
{
dcsPackage.Blobs = this.ExcludeUnnecessaryDcsBlobs(dcsPackage.Blobs);
this.CreateBlobPackage(dcsPackage);
}
```

**См. также**

Запросы к сервису интеграции
