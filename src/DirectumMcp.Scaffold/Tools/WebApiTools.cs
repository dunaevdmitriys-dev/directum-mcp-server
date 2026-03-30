using System.ComponentModel;
using DirectumMcp.Core.Services;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class WebApiTools
{
    [McpServerTool(Name = "scaffold_webapi")]
    [Description(
        "Создать WebAPI endpoint Directum RX: серверная функция с [Public(WebApiRequestType)]. " +
        "Доступна через HTTP GET/POST по URL /Integration/odata/{Module}/{Endpoint}. " +
        "GET: только примитивные параметры. POST: структуры через JSON body. " +
        "Для обычной функции используй scaffold_function.")]
    public async Task<string> ScaffoldWebApi(
        FunctionScaffoldService functionService,
        [Description("Путь к корню модуля")] string modulePath,
        [Description("Имя endpoint PascalCase")] string endpointName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("HTTP метод: Get или Post")] string httpMethod = "Get",
        [Description("Параметры: 'entityId:long,status:string'")] string parameters = "",
        [Description("Тип возвращаемого значения")] string returnType = "string",
        [Description("Описание endpoint")] string description = "")
    {
        var method = httpMethod.StartsWith("P", StringComparison.OrdinalIgnoreCase) ? "Post" : "Get";

        var bodyCode = method == "Get"
            ? "// TODO: Реализовать GET endpoint\nreturn \"ok\";"
            : "// TODO: Реализовать POST endpoint\nreturn \"ok\";";

        var result = await functionService.ScaffoldAsync(
            modulePath, endpointName, moduleName, returnType: returnType,
            parameters: parameters, side: "server", isPublic: true, isRemote: false,
            body: bodyCode,
            description: string.IsNullOrWhiteSpace(description) ? $"WebAPI {method} endpoint" : description);

        if (!result.Success)
            return $"**ОШИБКА**: {string.Join("; ", result.Errors)}";

        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        var csPath = Path.Combine(serverDir, "ModuleServerFunctions.cs");
        if (File.Exists(csPath))
        {
            var content = await File.ReadAllTextAsync(csPath);
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

            ### URL
            ```
            {(method == "Get"
                ? $"GET /Integration/odata/{moduleName.Replace(".", "")}/{endpointName}?$param=value"
                : $"POST /Integration/odata/{moduleName.Replace(".", "")}/{endpointName}")}
            ```

            ### Файлы
            {string.Join("\n", result.CreatedFiles.Concat(result.ModifiedFiles).Select(f => $"- `{f}`"))}
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
