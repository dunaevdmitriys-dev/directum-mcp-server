using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
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

        // Ensure CommonResponse PublicStructure exists in Module.mtd
        var commonResponseAdded = await EnsureCommonResponseStructure(modulePath, moduleName);

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
            {(commonResponseAdded ? "- `Module.mtd` — добавлена PublicStructure CommonResponse" : "")}

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

    /// <summary>
    /// Добавляет PublicStructure CommonResponse в Module.mtd если её ещё нет.
    /// Паттерн WebAPI: стандартная обёртка ответа {Success, Message, Data}.
    /// </summary>
    private static async Task<bool> EnsureCommonResponseStructure(string modulePath, string moduleName)
    {
        var mtdPath = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        if (!File.Exists(mtdPath))
            return false;

        var json = await File.ReadAllTextAsync(mtdPath);
        var node = System.Text.Json.Nodes.JsonNode.Parse(json);
        if (node is not System.Text.Json.Nodes.JsonObject root)
            return false;

        var structures = root["PublicStructures"]?.AsArray();
        if (structures == null)
        {
            structures = new System.Text.Json.Nodes.JsonArray();
            root["PublicStructures"] = structures;
        }

        // Check if CommonResponse already exists
        foreach (var s in structures)
        {
            if (s?["Name"]?.GetValue<string>() == "CommonResponse")
                return false;
        }

        var structGuid = Guid.NewGuid().ToString("D");
        var structure = System.Text.Json.Nodes.JsonNode.Parse($$"""
        {
          "NameGuid": "{{structGuid}}",
          "Name": "CommonResponse",
          "IsPublic": true,
          "Properties": [
            {
              "Name": "Success",
              "IsNullable": false,
              "TypeFullName": "global::System.Boolean"
            },
            {
              "Name": "Message",
              "IsNullable": true,
              "TypeFullName": "global::System.String"
            },
            {
              "Name": "Data",
              "IsNullable": true,
              "TypeFullName": "global::System.String"
            }
          ]
        }
        """);

        structures.Add(structure);
        await File.WriteAllTextAsync(mtdPath, node.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return true;
    }
}
