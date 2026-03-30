using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldFunctionTool
{
    private readonly FunctionScaffoldService _service = new();

    [McpServerTool(Name = "scaffold_function")]
    [Description("Создать серверную/клиентскую функцию: C# код + обновление PublicFunctions в Module.mtd.")]
    public async Task<string> ScaffoldFunction(
        [Description("Путь к корню модуля (директория с .Shared, .Server, .ClientBase)")] string modulePath,
        [Description("Имя функции в PascalCase")] string functionName,
        [Description("Полное имя модуля (например 'DirRX.CRM')")] string moduleName,
        [Description("Имя сущности (если функция entity-level, иначе пусто для module-level)")] string? entityName = null,
        [Description("Тип возвращаемого значения: void, string, bool, long, IQueryable<IDeal>, List<string>")] string returnType = "void",
        [Description("Параметры: 'entityId:long,name:string' или 'long entityId, string name'")] string parameters = "",
        [Description("Сторона: server, client, shared")] string side = "server",
        [Description("Доступна из других модулей (PublicFunctions)")] bool isPublic = false,
        [Description("Вызывается с клиента (Remote)")] bool isRemote = false,
        [Description("Тело функции (C# код). Если пусто — генерируется TODO-заглушка.")] string? body = null,
        [Description("Описание функции для XML doc comment")] string? description = null)
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var result = await _service.ScaffoldAsync(
            modulePath, functionName, moduleName, entityName,
            returnType, parameters, side, isPublic, isRemote, body, description);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        return result.ToMarkdown();
    }
}
