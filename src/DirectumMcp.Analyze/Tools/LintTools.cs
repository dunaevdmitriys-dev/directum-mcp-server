using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class LintTools
{
    [McpServerTool(Name = "lint_async_handlers")]
    [Description("Линтер AsyncHandlers: проверка retry стратегий, DelayPeriod, fan-out паттернов, параметров, обработчиков C#.")]
    public async Task<string> LintAsyncHandlers(
        [Description("Путь к модулю")] string path)
    {
        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория не найдена: `{path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Lint AsyncHandlers");
        sb.AppendLine();

        var mtdFiles = Directory.GetFiles(path, "Module.mtd", SearchOption.AllDirectories);
        int totalHandlers = 0, issues = 0;

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var moduleName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";

                if (!root.TryGetProperty("AsyncHandlers", out var handlers) || handlers.ValueKind != JsonValueKind.Array)
                    continue;

                if (handlers.GetArrayLength() == 0) continue;

                sb.AppendLine($"## {moduleName} ({handlers.GetArrayLength()} handlers)");
                sb.AppendLine();

                foreach (var handler in handlers.EnumerateArray())
                {
                    totalHandlers++;
                    var handlerName = handler.TryGetProperty("Name", out var hn) ? hn.GetString() ?? "?" : "?";
                    var delay = handler.TryGetProperty("DelayPeriod", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetInt32() : 0;
                    var strategy = handler.TryGetProperty("DelayStrategy", out var ds) ? ds.GetString() ?? "" : "";
                    var isGenerated = handler.TryGetProperty("IsHandlerGenerated", out var ig) && ig.GetBoolean();

                    var paramCount = 0;
                    var hasStringParam = false;
                    if (handler.TryGetProperty("Parameters", out var parms) && parms.ValueKind == JsonValueKind.Array)
                    {
                        paramCount = parms.GetArrayLength();
                        foreach (var p in parms.EnumerateArray())
                        {
                            var pt = p.TryGetProperty("ParameterType", out var ptv) ? ptv.GetString() ?? "" : "";
                            if (pt == "String") hasStringParam = true;
                        }
                    }

                    sb.AppendLine($"### {handlerName}");
                    sb.AppendLine($"Delay: {delay} мин | Strategy: {strategy} | Params: {paramCount}");

                    var handlerIssues = new List<string>();

                    // Lint 1: DelayPeriod слишком маленький для Exponential
                    if (strategy.Contains("Exponential") && delay < 5)
                    {
                        handlerIssues.Add("WARN: ExponentialDelay с DelayPeriod < 5 мин — первый retry будет очень быстрым");
                        issues++;
                    }

                    // Lint 3: Regular strategy с большим delay
                    if (strategy.Contains("Regular") && delay > 60)
                    {
                        handlerIssues.Add($"INFO: RegularDelay {delay} мин — каждый retry через {delay} мин, рассмотрите Exponential");
                    }

                    // Lint 4: Нет параметров — подозрительно
                    if (paramCount == 0)
                    {
                        handlerIssues.Add("WARN: Нет параметров — как определяется контекст обработки?");
                        issues++;
                    }

                    // Lint 5: String параметр для массива ID — потенциальный IdsString workaround
                    if (hasStringParam && (handlerName.Contains("Ids") || handlerName.Contains("Batch") || handlerName.Contains("Bulk")))
                    {
                        handlerIssues.Add("INFO: String параметр в batch-handler — вероятно IdsString split-паттерн (args.Ids.Split(','))");
                    }

                    // Lint 6: IsHandlerGenerated = false
                    if (!isGenerated)
                    {
                        handlerIssues.Add("WARN: IsHandlerGenerated=false — нет автогенерации обработчика, C# метод нужно создать вручную");
                        issues++;
                    }

                    // Lint 7: Fan-out detection
                    var executorName = $"Executor{handlerName}";
                    var hasExecutor = false;
                    foreach (var h2 in handlers.EnumerateArray())
                    {
                        var h2Name = h2.TryGetProperty("Name", out var h2n) ? h2n.GetString() ?? "" : "";
                        if (h2Name.StartsWith("Executor") || h2Name.EndsWith("Executor"))
                        {
                            if (handlerName.Contains(h2Name.Replace("Executor", "")) || h2Name.Contains(handlerName))
                                hasExecutor = true;
                        }
                    }

                    if (hasExecutor)
                    {
                        sb.AppendLine("**Паттерн:** Fan-out (Master → Executor)");
                    }

                    if (handlerIssues.Count > 0)
                    {
                        foreach (var issue in handlerIssues)
                            sb.AppendLine($"- {issue}");
                    }
                    else
                    {
                        sb.AppendLine("- OK");
                    }

                    sb.AppendLine();
                }
            }
            catch { }
        }

        // Check C# handlers exist
        sb.AppendLine("## Проверка C# обработчиков");
        var asyncCs = Directory.GetFiles(path, "*AsyncHandlers*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin")).ToArray();

        if (asyncCs.Length > 0)
        {
            sb.AppendLine($"Найдено {asyncCs.Length} файлов AsyncHandlers:");
            foreach (var f in asyncCs)
                sb.AppendLine($"- `{Path.GetFileName(f)}`");
        }
        else if (totalHandlers > 0)
        {
            sb.AppendLine("**FAIL**: AsyncHandlers определены в Module.mtd, но .cs файлов не найдено!");
            issues++;
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine($"**Handlers:** {totalHandlers} | **Issues:** {issues}");

        if (issues == 0 && totalHandlers > 0)
            sb.AppendLine("**Вердикт:** Все AsyncHandlers корректны");
        else if (issues > 0)
            sb.AppendLine($"**Вердикт:** {issues} замечаний — рекомендуется проверить");

        return sb.ToString();
    }
}
