using System.ComponentModel;
using System.Text.Json;
using DirectumMcp.Core.Pipeline;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.Deploy.Tools;

[McpServerToolType]
public class PipelineTools
{
    private readonly PipelineExecutor _executor = new();

    [McpServerTool(Name = "pipeline")]
    [Description("Оркестратор: выполнить цепочку инструментов последовательно с передачей контекста. Один вызов вместо 10+.")]
    public async Task<string> ExecutePipeline(
        [Description("JSON-массив шагов: [{\"tool\":\"scaffold_module\",\"params\":{...},\"id\":\"mod\",\"condition\":\"$prev.success == true\"}]. " +
                     "Доступные tools: scaffold_module, scaffold_entity, scaffold_function, scaffold_job, " +
                     "check_package, fix_package, build_dat, generate_initializer, preview_card. " +
                     "Placeholders: $prev.field, $steps[0].field, $steps[id].field. " +
                     "Пример: [{\"tool\":\"scaffold_module\",\"id\":\"mod\",\"params\":{\"outputPath\":\"/work\",\"moduleName\":\"Sales\",\"companyCode\":\"DirRX\"}}," +
                     "{\"tool\":\"scaffold_entity\",\"params\":{\"outputPath\":\"$steps[mod].modulePath\",\"entityName\":\"Deal\",\"moduleName\":\"DirRX.Sales\"}}]")]
        string stepsJson,
        CancellationToken cancellationToken = default)
    {
        PipelineStep[] steps;
        try
        {
            steps = ParseSteps(stepsJson);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Невалидный JSON: {ex.Message}";
        }

        if (steps.Length == 0)
            return "**ОШИБКА**: Массив шагов пуст.";

        var result = await _executor.ExecuteAsync(steps, ct: cancellationToken);
        return result.ToMarkdown();
    }

    private static PipelineStep[] ParseSteps(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.ValueKind != JsonValueKind.Array)
            throw new JsonException("Ожидается JSON-массив шагов.");

        var steps = new List<PipelineStep>();

        foreach (var stepEl in root.EnumerateArray())
        {
            var tool = stepEl.GetProperty("tool").GetString()
                       ?? throw new JsonException("Каждый шаг должен иметь поле 'tool'.");

            var paramsDict = new Dictionary<string, JsonElement>();
            if (stepEl.TryGetProperty("params", out var paramsEl) && paramsEl.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in paramsEl.EnumerateObject())
                    paramsDict[prop.Name] = prop.Value.Clone();
            }

            string? condition = null;
            if (stepEl.TryGetProperty("condition", out var condEl) && condEl.ValueKind == JsonValueKind.String)
                condition = condEl.GetString();

            string? id = null;
            if (stepEl.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                id = idEl.GetString();

            steps.Add(new PipelineStep
            {
                Tool = tool,
                Params = paramsDict,
                Condition = condition,
                Id = id
            });
        }

        return steps.ToArray();
    }
}
