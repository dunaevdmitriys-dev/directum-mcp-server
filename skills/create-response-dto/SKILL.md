---
description: "Создать PublicStructure DTO (CommonResponse, Request/Response) для WebAPI функций Directum RX"
---

# Создание Response DTO для WebAPI

Создаёт типизированные DTO-обёртки для WebAPI функций по паттерну Targets.

## Когда использовать
- Перед `/create-webapi` — чтобы endpoint возвращал структурированный ответ
- Когда WebAPI возвращает raw JSON строку — рефакторинг в DTO

## Алгоритм

### 1. Найти Module.mtd
Найди Module.mtd модуля и секцию `PublicStructures`.

### 2. Создать CommonResponse (если нет)

В Module.mtd → PublicStructures добавить:
```json
{
  "Name": "CommonResponse",
  "IsPublic": true,
  "Properties": [
    { "Name": "IsSuccess", "TypeFullName": "global::System.Boolean" },
    { "Name": "Message", "TypeFullName": "global::System.String" }
  ]
}
```

### 3. Создать Request/Response для endpoint

Для каждого WebAPI endpoint создать пару:
```json
{
  "Name": "{MethodName}Request",
  "IsPublic": true,
  "Properties": [
    { "Name": "EntityId", "TypeFullName": "global::System.Int64" }
  ]
},
{
  "Name": "{MethodName}Response",
  "IsPublic": true,
  "Properties": [
    { "Name": "IsSuccess", "TypeFullName": "global::System.Boolean" },
    { "Name": "Message", "TypeFullName": "global::System.String" },
    { "Name": "Payload", "TypeFullName": "global::System.String" }
  ]
}
```

### 4. Использование в ServerFunctions

```csharp
[Public(WebApiRequestType = RequestType.Post)]
public virtual Structures.Module.I{MethodName}Response {MethodName}(Structures.Module.I{MethodName}Request request)
{
    var response = Structures.Module.{MethodName}Response.Create();
    response.IsSuccess = true;

    try
    {
        // Бизнес-логика
        var entity = MyEntities.GetAll(e => e.Id == request.EntityId).FirstOrDefault();
        if (entity == null)
        {
            response.IsSuccess = false;
            response.Message = Resources.EntityNotFound;
            return response;
        }

        response.Payload = SerializeResult(entity);
    }
    catch (Exception ex)
    {
        response.IsSuccess = false;
        response.Message = ex.Message;
        Logger.WithLogger("MyModule").Error("{MethodName}: {0}", ex.Message);
    }

    return response;
}
```

### 5. Валидация
- `/validate-all` после изменений
- Проверить что PublicStructures в Module.mtd имеют уникальные имена

## Reference
- Targets: `DirRX.Targets.Shared/Module.mtd` → PublicStructures (60+ DTO)
- Agile: `DirRX.AgileBoards` → CommonResponse, TicketSavedResult
