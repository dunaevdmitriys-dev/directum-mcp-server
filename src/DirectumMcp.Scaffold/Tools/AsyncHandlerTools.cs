using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Scaffold.Tools;

[McpServerToolType]
public class AsyncHandlerTools
{
    [McpServerTool(Name = "scaffold_async_handler")]
    [Description(
        "Создать AsyncHandler: обновить Module.mtd + C# обработчик + resx. " +
        "Для фоновой обработки по событию. " +
        "Параметры типа: LongInteger, String, Boolean, DateTime, Double. " +
        "Для фонового задания по расписанию используй scaffold_job.")]
    public async Task<string> ScaffoldAsyncHandler(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("Имя обработчика PascalCase")] string handlerName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Параметры: 'DealId:LongInteger,ManagerId:LongInteger'")] string parameters = "",
        [Description("Задержка в минутах (по умолчанию 15)")] int delayPeriod = 15,
        [Description("Стратегия: Regular или Exponential")] string delayStrategy = "Regular")
    {
        var handlerGuid = Guid.NewGuid().ToString("D");
        var parsedParams = ParseParams(parameters);
        var strategy = delayStrategy.StartsWith("Exp", StringComparison.OrdinalIgnoreCase)
            ? "ExponentialDelayStrategy" : "RegularDelayStrategy";

        var paramsJson = new StringBuilder();
        for (int i = 0; i < parsedParams.Count; i++)
        {
            var p = parsedParams[i];
            var comma = i < parsedParams.Count - 1 ? "," : "";
            paramsJson.AppendLine($"        {{\n          \"NameGuid\": \"{Guid.NewGuid():D}\",\n          \"Name\": \"{p.Name}\",\n          \"ParameterType\": \"{p.Type}\"\n        }}{comma}");
        }

        var handlerJson = $$$"""
            {
              "NameGuid": "{{{handlerGuid}}}",
              "Name": "{{{handlerName}}}",
              "DelayPeriod": {{{delayPeriod}}},
              "DelayStrategy": "{{{strategy}}}",
              "IsHandlerGenerated": true,
              "Parameters": [
            {{{paramsJson}}}  ]
            }
            """;

        var mtdPath = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        if (!File.Exists(mtdPath))
            return $"**ОШИБКА**: Module.mtd не найден: `{mtdPath}`";

        var mtdJson = await File.ReadAllTextAsync(mtdPath);
        var node = JsonNode.Parse(mtdJson);
        if (node is not JsonObject root)
            return "**ОШИБКА**: Невалидный Module.mtd";

        var handlers = root["AsyncHandlers"]?.AsArray();
        if (handlers == null) { handlers = new JsonArray(); root["AsyncHandlers"] = handlers; }
        handlers.Add(JsonNode.Parse(handlerJson));

        await File.WriteAllTextAsync(mtdPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        Directory.CreateDirectory(serverDir);
        var handlerCsPath = Path.Combine(serverDir, "ModuleAsyncHandlers.cs");

        var csParamAccess = string.Join("\n            ", parsedParams.Select(p =>
            $"var {ToCamelCase(p.Name)} = args.{p.Name};"));

        var handlerCsSb = new StringBuilder();
        handlerCsSb.AppendLine("        /// <summary>");
        handlerCsSb.AppendLine($"        /// Асинхронный обработчик {handlerName}.");
        handlerCsSb.AppendLine("        /// </summary>");
        handlerCsSb.AppendLine($"        public virtual void {handlerName}(");
        handlerCsSb.AppendLine($"            {moduleName}.Server.AsyncHandlerInvokeArgs.{handlerName}InvokeArgs args)");
        handlerCsSb.AppendLine("        {");
        if (parsedParams.Count > 0)
            handlerCsSb.AppendLine($"            {csParamAccess}");
        handlerCsSb.AppendLine();
        handlerCsSb.AppendLine($"            Logger.Debug(\"{moduleName}: AsyncHandler {handlerName} запущен.\");");
        handlerCsSb.AppendLine("            // TODO: Реализовать логику обработчика");
        handlerCsSb.AppendLine("        }");
        var handlerCs = handlerCsSb.ToString();

        if (File.Exists(handlerCsPath))
        {
            var existing = await File.ReadAllTextAsync(handlerCsPath);
            var insertIdx = existing.LastIndexOf('}', existing.LastIndexOf('}') - 1);
            if (insertIdx > 0)
                await File.WriteAllTextAsync(handlerCsPath, existing[..insertIdx] + "\n" + handlerCs + "\n" + existing[insertIdx..]);
        }
        else
        {
            await File.WriteAllTextAsync(handlerCsPath,
                $"using System;\nusing System.Linq;\nusing Sungero.Core;\n\nnamespace {moduleName}.Server\n{{\n    partial class ModuleAsyncHandlers\n    {{\n{handlerCs}\n    }}\n}}");
        }

        // Update both .resx and .ru.resx
        foreach (var suffix in new[] { "ModuleSystem.resx", "ModuleSystem.ru.resx" })
        {
            var resxPath = Path.Combine(modulePath, $"{moduleName}.Shared", suffix);
            if (File.Exists(resxPath))
            {
                var xml = await File.ReadAllTextAsync(resxPath);
                var key = $"AsyncHandler_{handlerName}";
                if (!xml.Contains($"name=\"{key}\""))
                {
                    xml = Core.Services.JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
                        $"  <data name=\"{key}\" xml:space=\"preserve\">\n    <value>{handlerName}</value>\n  </data>");
                    await File.WriteAllTextAsync(resxPath, xml);
                }
            }
        }

        return $"""
            ## AsyncHandler создан

            **Имя:** {handlerName}
            **GUID:** {handlerGuid}
            **Задержка:** {delayPeriod} мин ({strategy})
            **Параметры:** {(parsedParams.Count > 0 ? string.Join(", ", parsedParams.Select(p => $"{p.Name}:{p.Type}")) : "нет")}

            ### Обновлённые файлы
            - `Module.mtd` — AsyncHandlers
            - `ModuleAsyncHandlers.cs` — обработчик
            - `ModuleSystem.resx` + `ModuleSystem.ru.resx` — AsyncHandler_{handlerName}
            """;
    }

    private static List<(string Name, string Type)> ParseParams(string parameters)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(parameters)) return result;
        foreach (var part in parameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx > 0)
                result.Add((part[..colonIdx].Trim(), part[(colonIdx + 1)..].Trim()));
        }
        return result;
    }

    private static string ToCamelCase(string name) =>
        string.IsNullOrEmpty(name) ? name : char.ToLowerInvariant(name[0]) + name[1..];
}
