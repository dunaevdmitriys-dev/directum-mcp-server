using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldWebApiTool
{
    private readonly FunctionScaffoldService _functionService = new();

    [McpServerTool(Name = "scaffold_webapi")]
    [Description("Создать WebAPI endpoint: серверная функция с [Public(WebApiRequestType)], доступная через HTTP GET/POST.")]
    public async Task<string> ScaffoldWebApi(
        [Description("Путь к корню модуля")] string modulePath,
        [Description("Имя endpoint PascalCase (например 'GetActiveDeals')")] string endpointName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("HTTP метод: Get или Post")] string httpMethod = "Get",
        [Description("Параметры: 'entityId:long,status:string'. GET — только примитивы, POST — любые")] string parameters = "",
        [Description("Тип возвращаемого значения")] string returnType = "string",
        [Description("Описание endpoint")] string description = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var method = httpMethod.StartsWith("P", StringComparison.OrdinalIgnoreCase) ? "Post" : "Get";

        // Generate function with WebApiRequestType attribute
        var bodyCode = method == "Get"
            ? "// TODO: Реализовать GET endpoint\nreturn \"ok\";"
            : "// TODO: Реализовать POST endpoint\nreturn \"ok\";";

        var result = await _functionService.ScaffoldAsync(
            modulePath: modulePath,
            functionName: endpointName,
            moduleName: moduleName,
            returnType: returnType,
            parameters: parameters,
            side: "server",
            isPublic: true,
            isRemote: false,
            body: bodyCode,
            description: string.IsNullOrWhiteSpace(description) ? $"WebAPI {method} endpoint" : description);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        // Now we need to patch the generated function to add WebApiRequestType
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        var csPath = Path.Combine(serverDir, "ModuleServerFunctions.cs");
        if (File.Exists(csPath))
        {
            var content = await File.ReadAllTextAsync(csPath);
            // Replace [Public] with [Public(WebApiRequestType = RequestType.Get/Post)]
            content = content.Replace(
                $"[Public]\n        public static {MapCsType(returnType)} {endpointName}",
                $"[Public(WebApiRequestType = RequestType.{method})]\n        public virtual {MapCsType(returnType)} {endpointName}");
            await File.WriteAllTextAsync(csPath, content);
        }

        return $"""
            ## WebAPI endpoint создан

            **Endpoint:** {endpointName}
            **Метод:** {method}
            **Возвращает:** {returnType}
            **Параметры:** {(string.IsNullOrEmpty(parameters) ? "нет" : parameters)}

            ### URL доступа
            ```
            {(method == "Get"
                ? $"GET http://server/Integration/odata/{moduleName.Replace(".", "")}/{endpointName}?$param=value"
                : $"POST http://server/Integration/odata/{moduleName.Replace(".", "")}/{endpointName}")}
            ```

            ### Созданные/изменённые файлы
            {string.Join("\n", result.CreatedFiles.Concat(result.ModifiedFiles).Select(f => $"- `{f}`"))}
            {(result.MtdUpdated ? "- `Module.mtd` — PublicFunctions" : "")}

            ### Правила WebAPI
            - GET: только примитивные параметры (string, int, long, bool)
            - POST: структуры через JSON body
            - Авторизация: Basic Auth через сервис интеграции
            - URL: /Integration/odata/{moduleName}/{endpointName}
            """;
    }

    private static string MapCsType(string type) => type switch
    {
        "string" => "string",
        "int" => "int",
        "long" => "long",
        "bool" => "bool",
        _ => type
    };
}
