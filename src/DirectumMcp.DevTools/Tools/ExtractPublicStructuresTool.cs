using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ExtractPublicStructuresTool
{
    [McpServerTool(Name = "extract_public_structures")]
    [Description("Извлечь все PublicStructures из Module.mtd: имена, свойства, типы, JSON-схема, C# interface. Для понимания DTO модуля.")]
    public async Task<string> ExtractPublicStructures(
        [Description("Путь к Module.mtd или директории модуля")] string path)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        var mtdPath = FindModuleMtd(path);
        if (mtdPath == null)
            return $"**ОШИБКА**: Module.mtd не найден в `{path}`";

        var json = await File.ReadAllTextAsync(mtdPath);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var moduleName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
        var companyCode = root.TryGetProperty("CompanyCode", out var cc) ? cc.GetString() ?? "" : "";
        var fullName = string.IsNullOrEmpty(companyCode) ? moduleName : $"{companyCode}.{moduleName}";

        if (!root.TryGetProperty("PublicStructures", out var structures) || structures.ValueKind != JsonValueKind.Array)
            return $"Module `{fullName}` не содержит PublicStructures.";

        var sb = new StringBuilder();
        sb.AppendLine($"# PublicStructures — {fullName}");
        sb.AppendLine();

        int totalStructures = 0, totalProperties = 0;

        foreach (var structure in structures.EnumerateArray())
        {
            totalStructures++;
            var structName = structure.TryGetProperty("Name", out var sn) ? sn.GetString() ?? "?" : "?";
            var isPublic = structure.TryGetProperty("IsPublic", out var ip) && ip.GetBoolean();
            var ns = structure.TryGetProperty("StructureNamespace", out var sns) ? sns.GetString() ?? "" : "";

            sb.AppendLine($"## {structName}{(isPublic ? " [Public]" : "")}");
            if (!string.IsNullOrEmpty(ns))
                sb.AppendLine($"Namespace: `{ns}`");
            sb.AppendLine();

            // Properties table
            if (structure.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            {
                sb.AppendLine("| Свойство | Тип | Nullable | List | Entity |");
                sb.AppendLine("|----------|-----|----------|------|--------|");

                foreach (var prop in props.EnumerateArray())
                {
                    totalProperties++;
                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "";
                    var typeFull = prop.TryGetProperty("TypeFullName", out var tf) ? tf.GetString() ?? "" : "";
                    var isNullable = prop.TryGetProperty("IsNullable", out var inl) && inl.GetBoolean();
                    var isList = prop.TryGetProperty("IsList", out var il) && il.GetBoolean();
                    var isEntity = prop.TryGetProperty("IsEntity", out var ie) && ie.GetBoolean();

                    var shortType = SimplifyType(typeFull);
                    sb.AppendLine($"| {propName} | `{shortType}` | {(isNullable ? "yes" : "")} | {(isList ? "yes" : "")} | {(isEntity ? "yes" : "")} |");
                }
                sb.AppendLine();
            }

            // C# interface
            sb.AppendLine("```csharp");
            sb.AppendLine($"// Использование:");
            sb.AppendLine($"var dto = Structures.Module.{structName}.Create();");
            if (structure.TryGetProperty("Properties", out var props2) && props2.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in props2.EnumerateArray())
                {
                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "";
                    sb.AppendLine($"dto.{propName} = ...;");
                }
            }
            sb.AppendLine("```");
            sb.AppendLine();

            // JSON example
            sb.AppendLine("<details><summary>JSON schema</summary>");
            sb.AppendLine();
            sb.AppendLine("```json");
            sb.AppendLine("{");
            if (structure.TryGetProperty("Properties", out var props3) && props3.ValueKind == JsonValueKind.Array)
            {
                var propList = new List<string>();
                foreach (var prop in props3.EnumerateArray())
                {
                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "";
                    var typeFull = prop.TryGetProperty("TypeFullName", out var tf) ? tf.GetString() ?? "" : "";
                    var jsonExample = TypeToJsonExample(typeFull);
                    propList.Add($"  \"{propName}\": {jsonExample}");
                }
                sb.AppendLine(string.Join(",\n", propList));
            }
            sb.AppendLine("}");
            sb.AppendLine("```");
            sb.AppendLine("</details>");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine($"**Структур:** {totalStructures} | **Свойств:** {totalProperties}");

        return sb.ToString();
    }

    private static string? FindModuleMtd(string path)
    {
        if (File.Exists(path) && path.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
            return path;

        if (Directory.Exists(path))
        {
            // Search in Shared subdirectory
            var candidates = Directory.GetFiles(path, "Module.mtd", SearchOption.AllDirectories);
            return candidates.FirstOrDefault();
        }
        return null;
    }

    private static string SimplifyType(string fullType)
    {
        return fullType
            .Replace("global::", "")
            .Replace("System.Collections.Generic.List<", "List<")
            .Replace("System.", "")
            .Replace("Sungero.Domain.Shared.", "");
    }

    private static string TypeToJsonExample(string fullType)
    {
        if (fullType.Contains("String")) return "\"text\"";
        if (fullType.Contains("Int32") || fullType.Contains("Int64")) return "0";
        if (fullType.Contains("Double") || fullType.Contains("Decimal")) return "0.0";
        if (fullType.Contains("Boolean")) return "false";
        if (fullType.Contains("DateTime")) return "\"2026-01-01T00:00:00Z\"";
        if (fullType.Contains("Guid")) return "\"00000000-0000-0000-0000-000000000000\"";
        if (fullType.Contains("List<")) return "[]";
        return "null";
    }
}
