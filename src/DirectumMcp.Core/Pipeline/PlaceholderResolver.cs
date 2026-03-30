using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Services;

namespace DirectumMcp.Core.Pipeline;

/// <summary>
/// Resolves $prev and $steps[N] placeholders in pipeline step parameters.
/// </summary>
public static class PlaceholderResolver
{
    private static readonly Regex PrevPattern = new(@"\$prev\.(\w+)", RegexOptions.Compiled);
    private static readonly Regex StepsByIndexPattern = new(@"\$steps\[(\d+)\]\.(\w+)", RegexOptions.Compiled);
    private static readonly Regex StepsByIdPattern = new(@"\$steps\[(\w+)\]\.(\w+)", RegexOptions.Compiled);

    /// <summary>
    /// Resolves all placeholders in parameter values using previous step results.
    /// </summary>
    public static Dictionary<string, JsonElement> Resolve(
        Dictionary<string, JsonElement> parameters,
        List<StepContext> completedSteps,
        int currentIndex)
    {
        var resolved = new Dictionary<string, JsonElement>();

        foreach (var (key, value) in parameters)
        {
            if (value.ValueKind == JsonValueKind.String)
            {
                var strVal = value.GetString() ?? "";
                if (strVal.Contains('$'))
                {
                    var resolvedStr = ResolveString(strVal, completedSteps, currentIndex);
                    resolved[key] = JsonDocument.Parse($"\"{EscapeJson(resolvedStr)}\"").RootElement.Clone();
                    continue;
                }
            }
            resolved[key] = value;
        }

        return resolved;
    }

    /// <summary>
    /// Evaluates a simple condition expression like "$prev.success == false".
    /// </summary>
    public static bool EvaluateCondition(string condition, List<StepContext> completedSteps, int currentIndex)
    {
        // Parse: $prev.field == value  or  $prev.field > 0
        var parts = condition.Split(new[] { "==", "!=", ">", "<" }, StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            Console.Error.WriteLine($"WARNING: Cannot parse pipeline condition '{condition}' — expected format: '$prev.field == value'. Executing step anyway.");
            return true;
        }

        var leftRaw = parts[0].Trim();
        var rightRaw = parts[1].Trim();
        var op = condition.Contains("==") ? "==" :
                 condition.Contains("!=") ? "!=" :
                 condition.Contains(">") ? ">" : "<";

        var leftValue = ResolveString(leftRaw, completedSteps, currentIndex);

        return op switch
        {
            "==" => string.Equals(leftValue, rightRaw, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(leftValue, rightRaw, StringComparison.OrdinalIgnoreCase),
            ">" => double.TryParse(leftValue, out var lv) && double.TryParse(rightRaw, out var rv) && lv > rv,
            "<" => double.TryParse(leftValue, out var lv2) && double.TryParse(rightRaw, out var rv2) && lv2 < rv2,
            _ => true
        };
    }

    private static string ResolveString(string input, List<StepContext> steps, int currentIndex)
    {
        var result = input;

        // $prev.field → value from previous step
        result = PrevPattern.Replace(result, match =>
        {
            var field = match.Groups[1].Value;
            if (currentIndex > 0 && currentIndex - 1 < steps.Count)
                return GetFieldFromResult(steps[currentIndex - 1].Result, field);
            return match.Value;
        });

        // $steps[0].field → value from step by index
        result = StepsByIndexPattern.Replace(result, match =>
        {
            if (int.TryParse(match.Groups[1].Value, out var idx) && idx < steps.Count)
                return GetFieldFromResult(steps[idx].Result, match.Groups[2].Value);
            return match.Value;
        });

        // $steps[id].field → value from step by ID
        result = StepsByIdPattern.Replace(result, match =>
        {
            var id = match.Groups[1].Value;
            var step = steps.FirstOrDefault(s => s.Id == id);
            if (step != null)
                return GetFieldFromResult(step.Result, match.Groups[2].Value);
            return match.Value;
        });

        return result;
    }

    private static string GetFieldFromResult(ServiceResult? result, string field)
    {
        if (result == null) return "";

        // Serialize to JSON and extract field
        try
        {
            var json = result.ToJson();
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(field, out var prop))
            {
                return prop.ValueKind switch
                {
                    JsonValueKind.String => prop.GetString() ?? "",
                    JsonValueKind.Number => prop.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => prop.GetRawText()
                };
            }
        }
        catch { }

        // Fallback: try common properties directly
        return field.ToLowerInvariant() switch
        {
            "success" => result.Success.ToString().ToLowerInvariant(),
            "modulepath" when result is ScaffoldModuleResult m => m.ModulePath,
            "moduleguid" when result is ScaffoldModuleResult m => m.ModuleGuid,
            "fullname" when result is ScaffoldModuleResult m => m.FullName,
            "outputpath" when result is ScaffoldEntityResult e => e.OutputPath,
            "entityguid" when result is ScaffoldEntityResult e => e.EntityGuid,
            "outputpath" when result is BuildDatResult b => b.OutputPath,
            "packagepath" when result is ValidatePackageResult v => v.PackagePath,
            "passedcount" when result is ValidatePackageResult v => v.PassedCount.ToString(),
            "failedcount" when result is ValidatePackageResult v => v.FailedCount.ToString(),
            _ => ""
        };
    }

    private static string EscapeJson(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    public record StepContext(string? Id, ServiceResult? Result, string Status);
}
