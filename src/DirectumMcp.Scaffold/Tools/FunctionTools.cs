using System.ComponentModel;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class FunctionTools
{
    [McpServerTool(Name = "scaffold_function")]
    [Description(
        "Создать серверную/клиентскую функцию: C# код + обновление PublicFunctions в Module.mtd. " +
        "Для entity-level функции укажи entityName. Для module-level оставь пустым. " +
        "Для WebAPI endpoint используй scaffold_webapi вместо этого.")]
    public async Task<string> ScaffoldFunction(
        FunctionScaffoldService service,
        [Description("Путь к корню модуля")] string modulePath,
        [Description("Имя функции в PascalCase")] string functionName,
        [Description("Полное имя модуля (например 'DirRX.CRM')")] string moduleName,
        [Description("Имя сущности (для entity-level, иначе пусто)")] string? entityName = null,
        [Description("Тип возврата: void, string, bool, long, IQueryable<IDeal>")] string returnType = "void",
        [Description("Параметры: 'entityId:long,name:string'")] string parameters = "",
        [Description("Сторона: server, client, shared")] string side = "server",
        [Description("Доступна из других модулей")] bool isPublic = false,
        [Description("Вызывается с клиента (Remote)")] bool isRemote = false,
        [Description("Тело функции (C# код)")] string? body = null,
        [Description("Описание для XML doc comment")] string? description = null)
    {
        var result = await service.ScaffoldAsync(
            modulePath, functionName, moduleName, entityName,
            returnType, parameters, side, isPublic, isRemote, body, description);

        return result.Success ? result.ToMarkdown() : $"**ОШИБКА**: {string.Join("; ", result.Errors)}";
    }
}
