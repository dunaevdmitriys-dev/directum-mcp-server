using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class GenerateRouteSchemeTool
{
    [McpServerTool(Name = "generate_routescheme")]
    [Description("Генерация RouteScheme для workflow: блоки, переходы, условия. Обновляет Module.mtd Blocks и генерирует RouteScheme.xml.")]
    public async Task<string> GenerateRouteScheme(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Имя задачи (Task), для которой создаётся RouteScheme")] string taskName,
        [Description("Блоки через точку с запятой: 'Start:script;CreateAssignment:assignment;ProcessResult:script;Notify:notice'. Типы: script, assignment, notice, monitoring")] string blocks = "",
        [Description("Переходы: 'Start→CreateAssignment;CreateAssignment→ProcessResult;ProcessResult→Notify'. Формат: Блок1→Блок2 или Блок1→Блок2:Условие")] string transitions = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var parsedBlocks = ParseBlocks(blocks);
        var parsedTransitions = ParseTransitions(transitions);

        if (parsedBlocks.Count == 0)
        {
            // Default simple workflow
            parsedBlocks = [
                new BlockDef("Start", "script", Guid.NewGuid().ToString("D")),
                new BlockDef("CreateAssignment", "assignment", Guid.NewGuid().ToString("D")),
                new BlockDef("ProcessResult", "script", Guid.NewGuid().ToString("D")),
                new BlockDef("End", "script", Guid.NewGuid().ToString("D"))
            ];
            parsedTransitions = [
                ("Start", "CreateAssignment", ""),
                ("CreateAssignment", "ProcessResult", ""),
                ("ProcessResult", "End", "")
            ];
        }

        var createdFiles = new List<string>();

        // 1. Update Module.mtd — add Blocks
        var mtdPath = Path.Combine(modulePath, $"{moduleName}.Shared", "Module.mtd");
        if (File.Exists(mtdPath))
        {
            var mtdJson = await File.ReadAllTextAsync(mtdPath);
            var node = JsonNode.Parse(mtdJson);
            if (node is JsonObject root)
            {
                var blocksArray = root["Blocks"]?.AsArray();
                if (blocksArray == null) { blocksArray = new JsonArray(); root["Blocks"] = blocksArray; }

                foreach (var block in parsedBlocks)
                {
                    var blockType = block.Type switch
                    {
                        "assignment" => "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Workflow.Shared",
                        "notice" => "Sungero.Metadata.NoticeBlockMetadata, Sungero.Workflow.Shared",
                        "monitoring" => "Sungero.Metadata.MonitoringBlockMetadata, Sungero.Workflow.Shared",
                        _ => "Sungero.Metadata.ScriptBlockMetadata, Sungero.Workflow.Shared"
                    };

                    var blockObj = new JsonObject
                    {
                        ["$type"] = blockType,
                        ["NameGuid"] = block.Guid,
                        ["Name"] = block.Name,
                        ["HandledEvents"] = new JsonArray($"{block.Name}Execute"),
                        ["Properties"] = new JsonArray(),
                        ["OutProperties"] = new JsonArray()
                    };

                    // Add ExecutionResult enum for script blocks
                    if (block.Type == "script")
                    {
                        var outProps = blockObj["OutProperties"]!.AsArray();
                        outProps.Add(new JsonObject
                        {
                            ["$type"] = "Sungero.Metadata.EnumBlockPropertyMetadata, Sungero.Metadata",
                            ["NameGuid"] = Guid.NewGuid().ToString("D"),
                            ["Name"] = "ExecutionResult",
                            ["DirectValues"] = new JsonArray(
                                new JsonObject { ["NameGuid"] = Guid.NewGuid().ToString("D"), ["Name"] = "Success", ["Code"] = "Success" },
                                new JsonObject { ["NameGuid"] = Guid.NewGuid().ToString("D"), ["Name"] = "Error", ["Code"] = "Error" }
                            )
                        });
                    }

                    blocksArray.Add(blockObj);
                }

                await File.WriteAllTextAsync(mtdPath, node.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
                createdFiles.Add("Module.mtd (Blocks updated)");
            }
        }

        // 2. Generate RouteScheme.xml
        var routeXml = GenerateRouteXml(taskName, parsedBlocks, parsedTransitions);
        var sharedDir = Path.Combine(modulePath, $"{moduleName}.Shared", taskName);
        Directory.CreateDirectory(sharedDir);
        var routePath = Path.Combine(sharedDir, "RouteScheme.xml");
        await File.WriteAllTextAsync(routePath, routeXml);
        createdFiles.Add($"{taskName}/RouteScheme.xml");

        // 3. Generate block handlers
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        Directory.CreateDirectory(serverDir);
        var handlersCs = GenerateBlockHandlersCs(moduleName, taskName, parsedBlocks);
        var handlerPath = Path.Combine(serverDir, $"{taskName}BlockHandlers.cs");
        await File.WriteAllTextAsync(handlerPath, handlersCs);
        createdFiles.Add($"{taskName}BlockHandlers.cs");

        // Report
        var sb = new StringBuilder();
        sb.AppendLine("## RouteScheme создана");
        sb.AppendLine();
        sb.AppendLine($"**Задача:** {taskName}");
        sb.AppendLine($"**Блоков:** {parsedBlocks.Count}");
        sb.AppendLine($"**Переходов:** {parsedTransitions.Count}");
        sb.AppendLine();

        sb.AppendLine("### Блоки");
        foreach (var b in parsedBlocks)
            sb.AppendLine($"- [{b.Type}] **{b.Name}** (GUID: {b.Guid[..8]}...)");
        sb.AppendLine();

        sb.AppendLine("### Маршрут");
        foreach (var (from, to, cond) in parsedTransitions)
        {
            var condText = string.IsNullOrEmpty(cond) ? "" : $" ({cond})";
            sb.AppendLine($"- {from} → {to}{condText}");
        }
        sb.AppendLine();

        sb.AppendLine($"### Файлы ({createdFiles.Count})");
        foreach (var f in createdFiles) sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine("1. Реализуйте логику в BlockHandlers");
        sb.AppendLine("2. validate_workflow для проверки маршрута");
        sb.AppendLine("3. Для сложных условий — добавьте ExpressionElement свойства");

        return sb.ToString();
    }

    private static string GenerateRouteXml(string taskName, List<BlockDef> blocks, List<(string From, string To, string Condition)> transitions)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        sb.AppendLine($"<RouteScheme Name=\"{taskName}RouteScheme\">");

        // Blocks
        int x = 100, y = 50;
        foreach (var block in blocks)
        {
            sb.AppendLine($"  <Block Id=\"{block.Guid}\" Name=\"{block.Name}\" Type=\"{block.Type}\" X=\"{x}\" Y=\"{y}\" />");
            y += 120;
        }

        // Transitions
        foreach (var (from, to, condition) in transitions)
        {
            var fromBlock = blocks.FirstOrDefault(b => b.Name == from);
            var toBlock = blocks.FirstOrDefault(b => b.Name == to);
            if (fromBlock == null || toBlock == null) continue;

            sb.Append($"  <Transition From=\"{fromBlock.Guid}\" To=\"{toBlock.Guid}\"");
            if (!string.IsNullOrEmpty(condition))
                sb.Append($" Condition=\"{condition}\"");
            sb.AppendLine(" />");
        }

        sb.AppendLine("</RouteScheme>");
        return sb.ToString();
    }

    private static string GenerateBlockHandlersCs(string moduleName, string taskName, List<BlockDef> blocks)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine("using Sungero.Workflow;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {taskName}BlockHandlers");
        sb.AppendLine("    {");

        foreach (var block in blocks)
        {
            sb.AppendLine($"        /// <summary>");
            sb.AppendLine($"        /// Блок: {block.Name} ({block.Type}).");
            sb.AppendLine($"        /// </summary>");

            if (block.Type == "assignment")
            {
                sb.AppendLine($"        public virtual void {block.Name}Start(");
                sb.AppendLine($"            {moduleName}.Server.{taskName}BlockHandlers.{block.Name}StartEventArgs e)");
                sb.AppendLine("        {");
                sb.AppendLine("            // TODO: Настройте задание — исполнитель, тема, срок");
                sb.AppendLine("            // e.Block.Performers.Add(performer);");
                sb.AppendLine("        }");
            }
            else
            {
                sb.AppendLine($"        public virtual void {block.Name}Execute(");
                sb.AppendLine($"            {moduleName}.Server.{taskName}BlockHandlers.{block.Name}ExecuteEventArgs e)");
                sb.AppendLine("        {");
                if (block.Type == "script")
                {
                    sb.AppendLine("            // TODO: Реализуйте логику блока");
                    sb.AppendLine("            e.Block.ExecutionResult = ExecutionResult.Success;");
                }
                else
                {
                    sb.AppendLine("            // TODO: Реализуйте логику блока");
                }
                sb.AppendLine("        }");
            }
            sb.AppendLine();
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static List<BlockDef> ParseBlocks(string blocks)
    {
        var result = new List<BlockDef>();
        if (string.IsNullOrWhiteSpace(blocks)) return result;

        foreach (var part in blocks.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            var name = colonIdx > 0 ? part[..colonIdx].Trim() : part.Trim();
            var type = colonIdx > 0 ? part[(colonIdx + 1)..].Trim().ToLowerInvariant() : "script";
            result.Add(new BlockDef(name, type, Guid.NewGuid().ToString("D")));
        }
        return result;
    }

    private static List<(string From, string To, string Condition)> ParseTransitions(string transitions)
    {
        var result = new List<(string, string, string)>();
        if (string.IsNullOrWhiteSpace(transitions)) return result;

        foreach (var part in transitions.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var arrow = part.IndexOf('→');
            if (arrow < 0) arrow = part.IndexOf("->");
            if (arrow <= 0) continue;

            var from = part[..arrow].Trim();
            var rest = part[(arrow + (part[arrow] == '→' ? 1 : 2))..].Trim();

            var condIdx = rest.IndexOf(':');
            var to = condIdx > 0 ? rest[..condIdx].Trim() : rest;
            var condition = condIdx > 0 ? rest[(condIdx + 1)..].Trim() : "";

            result.Add((from, to, condition));
        }
        return result;
    }

    private record BlockDef(string Name, string Type, string Guid);
}
