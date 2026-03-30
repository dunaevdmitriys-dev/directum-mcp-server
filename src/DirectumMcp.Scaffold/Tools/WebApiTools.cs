using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
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

        // Ensure CommonResponse PublicStructure exists in Module.mtd
        var commonResponseAdded = await EnsureCommonResponseStructure(modulePath, moduleName);

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
            {(commonResponseAdded ? "- `Module.mtd` — добавлена PublicStructure CommonResponse" : "")}
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
