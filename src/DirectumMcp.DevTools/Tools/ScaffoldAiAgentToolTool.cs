using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldAiAgentToolTool
{
    [McpServerTool(Name = "scaffold_ai_agent_tool")]
    [Description("Создать AIAgentTool: AsyncHandler для обработки запросов AI-агента. Обновляет Module.mtd + C# обработчик.")]
    public async Task<string> ScaffoldAiAgentTool(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("Имя инструмента PascalCase (например 'SearchDocumentsTool')")] string toolName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Описание что делает инструмент")] string description = "",
        [Description("Входные параметры: 'Query:String,MaxResults:LongInteger'")] string inputParameters = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var handlerGuid = Guid.NewGuid().ToString("D");
        var parsedParams = new List<(string Name, string Type)> {
            ("ToolCallId", "String"),
            ("InputJson", "String")
        };

        // Add custom params
        if (!string.IsNullOrWhiteSpace(inputParameters))
        {
            foreach (var part in inputParameters.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var idx = part.IndexOf(':');
                if (idx > 0)
                    parsedParams.Add((part[..idx].Trim(), part[(idx + 1)..].Trim()));
            }
        }

        // 1. Update Module.mtd — add AsyncHandler
        var mtdPath = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        if (!File.Exists(mtdPath))
            return $"**ОШИБКА**: Module.mtd не найден: `{mtdPath}`";

        var mtdJson = await File.ReadAllTextAsync(mtdPath);
        var node = JsonNode.Parse(mtdJson);
        if (node is not JsonObject root)
            return "**ОШИБКА**: Невалидный Module.mtd";

        var handlers = root["AsyncHandlers"]?.AsArray();
        if (handlers == null) { handlers = new JsonArray(); root["AsyncHandlers"] = handlers; }

        var paramsArray = new JsonArray();
        foreach (var p in parsedParams)
        {
            paramsArray.Add(new JsonObject
            {
                ["NameGuid"] = Guid.NewGuid().ToString("D"),
                ["Name"] = p.Name,
                ["ParameterType"] = p.Type
            });
        }

        handlers.Add(new JsonObject
        {
            ["NameGuid"] = handlerGuid,
            ["Name"] = $"{toolName}Handler",
            ["DelayPeriod"] = 1,
            ["DelayStrategy"] = "RegularDelayStrategy",
            ["IsHandlerGenerated"] = true,
            ["MaxRetryCount"] = 100,
            ["Parameters"] = paramsArray
        });

        await File.WriteAllTextAsync(mtdPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        // 2. Generate C# handler
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        Directory.CreateDirectory(serverDir);

        var handlerCs = new StringBuilder();
        handlerCs.AppendLine("using System;");
        handlerCs.AppendLine("using System.Text.Json;");
        handlerCs.AppendLine("using Sungero.Core;");
        handlerCs.AppendLine();
        handlerCs.AppendLine($"namespace {moduleName}.Server");
        handlerCs.AppendLine("{");
        handlerCs.AppendLine("    partial class ModuleAsyncHandlers");
        handlerCs.AppendLine("    {");
        handlerCs.AppendLine("        /// <summary>");
        handlerCs.AppendLine($"        /// AI Agent Tool: {toolName}.");
        if (!string.IsNullOrWhiteSpace(description))
            handlerCs.AppendLine($"        /// {description}");
        handlerCs.AppendLine("        /// </summary>");
        handlerCs.AppendLine($"        public virtual void {toolName}Handler(");
        handlerCs.AppendLine($"            {moduleName}.Server.AsyncHandlerInvokeArgs.{toolName}HandlerInvokeArgs args)");
        handlerCs.AppendLine("        {");
        handlerCs.AppendLine("            var toolCallId = args.ToolCallId;");
        handlerCs.AppendLine("            var inputJson = args.InputJson;");
        handlerCs.AppendLine();
        handlerCs.AppendLine($"            Logger.Debug(\"{moduleName}: AI Tool {toolName} вызван, callId={{0}}\", toolCallId);");
        handlerCs.AppendLine();
        handlerCs.AppendLine("            try");
        handlerCs.AppendLine("            {");
        handlerCs.AppendLine("                // TODO: Десериализовать inputJson и выполнить действие");
        handlerCs.AppendLine("                // var input = JsonSerializer.Deserialize<InputModel>(inputJson);");
        handlerCs.AppendLine("                // var result = ProcessToolCall(input);");
        handlerCs.AppendLine("                // SaveToolResult(toolCallId, result);");
        handlerCs.AppendLine("            }");
        handlerCs.AppendLine("            catch (Exception ex)");
        handlerCs.AppendLine("            {");
        handlerCs.AppendLine($"                Logger.Error(\"{moduleName}: AI Tool {toolName} ошибка: {{0}}\", ex.Message);");
        handlerCs.AppendLine("                args.Retry = true;");
        handlerCs.AppendLine("            }");
        handlerCs.AppendLine("        }");
        handlerCs.AppendLine("    }");
        handlerCs.AppendLine("}");

        var handlerPath = Path.Combine(serverDir, $"{toolName}Handler.cs");
        await File.WriteAllTextAsync(handlerPath, handlerCs.ToString());

        return $"""
            ## AI Agent Tool создан

            **Имя:** {toolName}
            **Handler:** {toolName}Handler
            **GUID:** {handlerGuid}
            **Описание:** {(string.IsNullOrWhiteSpace(description) ? toolName : description)}

            ### Параметры
            {string.Join("\n", parsedParams.Select(p => $"- {p.Name}: {p.Type}"))}

            ### Созданные/обновлённые файлы
            - `Module.mtd` — AsyncHandlers + {toolName}Handler
            - `{toolName}Handler.cs` — обработчик AI-вызова

            ### Как вызвать
            ```csharp
            // Из AI-агента:
            {moduleName}.PublicFunctions.Module.CreateAsyncHandler(
                "{toolName}Handler", toolCallId, inputJson);
            ```
            """;
    }
}
