using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class ExpressionTools
{
    private static readonly string[] ExpressionFunctionTypes =
    {
        "GetFilteredEntities", "GetPredefinedValues", "GetDisplayName", "Validate", "Calculate"
    };

    [McpServerTool(Name = "validate_expression_elements")]
    [Description("Валидация ExpressionElement функций в workflow-блоках: проверка 5 типов функций (GetFilteredEntities, GetPredefinedValues, GetDisplayName, Validate, Calculate).")]
    public async Task<string> ValidateExpressionElements(
        [Description("Путь к модулю или .mtd файлу")] string path)
    {
        var mtdFiles = new List<string>();
        if (File.Exists(path) && path.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
            mtdFiles.Add(path);
        else if (Directory.Exists(path))
            mtdFiles.AddRange(Directory.GetFiles(path, "*.mtd", SearchOption.AllDirectories));
        else
            return $"**ОШИБКА**: Путь не найден: `{path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Валидация ExpressionElement");
        sb.AppendLine();

        int totalFound = 0, totalIssues = 0;

        foreach (var mtdFile in mtdFiles)
        {
            string json;
            try { json = await File.ReadAllTextAsync(mtdFile); }
            catch { continue; }

            if (!json.Contains("ExpressionElement", StringComparison.OrdinalIgnoreCase) &&
                !json.Contains("Expression", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";

                // Check Blocks for ExpressionElement properties
                if (root.TryGetProperty("Blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
                {
                    foreach (var block in blocks.EnumerateArray())
                    {
                        var blockName = block.TryGetProperty("Name", out var bn) ? bn.GetString() ?? "?" : "?";

                        foreach (var propArrayName in new[] { "Properties", "OutProperties" })
                        {
                            if (!block.TryGetProperty(propArrayName, out var props) || props.ValueKind != JsonValueKind.Array)
                                continue;

                            foreach (var prop in props.EnumerateArray())
                            {
                                var typeName = prop.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                                if (!typeName.Contains("Expression", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                totalFound++;
                                var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                                // Check HandledEvents for required expression functions
                                var handledEvents = new List<string>();
                                if (prop.TryGetProperty("HandledEvents", out var he) && he.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var evt in he.EnumerateArray())
                                        handledEvents.Add(evt.GetString() ?? "");
                                }

                                sb.AppendLine($"## {entityName} → {blockName} → {propName}");
                                sb.AppendLine($"Тип: `{typeName}`");
                                sb.AppendLine();

                                foreach (var funcType in ExpressionFunctionTypes)
                                {
                                    var expectedEvent = $"{propName}{funcType}";
                                    var found = handledEvents.Any(e => e.Contains(funcType, StringComparison.OrdinalIgnoreCase));
                                    var status = found ? "OK" : "MISSING";
                                    if (!found) totalIssues++;
                                    sb.AppendLine($"- [{status}] {funcType}: {(found ? "обработчик найден" : "обработчик отсутствует")}");
                                }
                                sb.AppendLine();
                            }
                        }
                    }
                }
            }
            catch { }
        }

        if (totalFound == 0)
        {
            sb.AppendLine("ExpressionElement свойства не найдены.");
        }
        else
        {
            sb.AppendLine("---");
            sb.AppendLine($"**Найдено ExpressionElement:** {totalFound}");
            sb.AppendLine($"**Проблем:** {totalIssues}");
        }

        return sb.ToString();
    }
}
