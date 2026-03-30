using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateWorkflowTool
{
    [McpServerTool(Name = "validate_workflow")]
    [Description("Валидация RouteScheme: мёртвые блоки, тупики, переходы без условий.")]
    public async Task<string> ValidateWorkflow(
        [Description("Путь к .mtd файлу задачи или директории с .mtd файлами")] string path,
        [Description("Фильтр серьёзности: all | error | warning (по умолчанию: all)")] string? severity = "all")
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        var severityFilter = (severity ?? "all").ToLowerInvariant();

        string[] mtdFiles;

        if (File.Exists(path) && path.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
        {
            mtdFiles = new[] { path };
        }
        else if (Directory.Exists(path))
        {
            mtdFiles = Directory.GetFiles(path, "*.mtd", SearchOption.AllDirectories);
        }
        else
        {
            return $"**ОШИБКА**: Путь не найден: `{path}`\nУкажите путь к .mtd файлу задачи или директории.";
        }

        if (mtdFiles.Length == 0)
            return $"**ОШИБКА**: В директории `{path}` не найдено ни одного .mtd файла.";

        var allFileResults = new List<WorkflowFileResult>();

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Only process task/assignment metadata with RouteScheme
                var blocks = ExtractBlocks(root);
                if (blocks.Count == 0)
                    continue;

                var issues = new List<WorkflowIssue>();

                // Find RouteHandlers .cs file in the same directory
                var dir = Path.GetDirectoryName(mtdFile) ?? "";
                var handlerFiles = Directory.GetFiles(dir, "*RouteHandlers.cs", SearchOption.TopDirectoryOnly);
                var handlerContent = await LoadHandlerContent(handlerFiles);

                // Run all 4 checks
                issues.AddRange(Check1_DeadBlocks(blocks));
                issues.AddRange(Check2_ConditionBlocksWithoutConditions(blocks));
                issues.AddRange(Check3_HandlersWithoutCode(blocks, handlerContent));
                issues.AddRange(Check4_EmptyBlocks(blocks));

                // Apply severity filter
                var filtered = severityFilter switch
                {
                    "error" => issues.Where(i => i.Severity == IssueSeverity.Error).ToList(),
                    "warning" => issues.Where(i => i.Severity == IssueSeverity.Warning).ToList(),
                    _ => issues
                };

                if (filtered.Count > 0)
                {
                    allFileResults.Add(new WorkflowFileResult(mtdFile, blocks.Count, filtered));
                }
            }
            catch (JsonException ex)
            {
                allFileResults.Add(new WorkflowFileResult(
                    mtdFile, 0,
                    new List<WorkflowIssue>
                    {
                        new(IssueSeverity.Error, "ParseError", $"Ошибка парсинга JSON: {ex.Message}", null)
                    }));
            }
        }

        return FormatReport(path, mtdFiles.Length, allFileResults, severityFilter);
    }

    // ---------- Block extraction ----------

    /// <summary>
    /// Extracts blocks from RouteScheme.Blocks or root-level Blocks array.
    /// Returns a list of parsed block descriptors.
    /// </summary>
    public static List<RouteBlock> ExtractBlocks(JsonElement root)
    {
        JsonElement? blocksElement = null;

        // Try RouteScheme.Blocks first
        if (root.TryGetProperty("RouteScheme", out var routeScheme) &&
            routeScheme.ValueKind == JsonValueKind.Object &&
            routeScheme.TryGetProperty("Blocks", out var rsBlocks) &&
            rsBlocks.ValueKind == JsonValueKind.Array)
        {
            blocksElement = rsBlocks;
        }
        // Fallback to root-level Blocks
        else if (root.TryGetProperty("Blocks", out var rootBlocks) &&
                 rootBlocks.ValueKind == JsonValueKind.Array)
        {
            blocksElement = rootBlocks;
        }

        if (blocksElement == null)
            return new List<RouteBlock>();

        var blocks = new List<RouteBlock>();

        foreach (var blockEl in blocksElement.Value.EnumerateArray())
        {
            var guid = GetString(blockEl, "NameGuid");
            var name = GetString(blockEl, "Name");
            var blockType = GetString(blockEl, "BlockType");
            var typeStr = GetString(blockEl, "$type");
            var generateHandler = blockEl.TryGetProperty("GenerateHandler", out var gh) &&
                                  gh.ValueKind == JsonValueKind.True;

            if (string.IsNullOrEmpty(guid))
                continue;

            var connectors = new List<RouteConnector>();
            if (blockEl.TryGetProperty("Connectors", out var cons) && cons.ValueKind == JsonValueKind.Array)
            {
                foreach (var con in cons.EnumerateArray())
                {
                    var toBlock = GetString(con, "ToBlock");
                    var condition = GetString(con, "Condition");
                    connectors.Add(new RouteConnector(toBlock, condition));
                }
            }

            blocks.Add(new RouteBlock(guid, name, blockType, typeStr, generateHandler, connectors));
        }

        return blocks;
    }

    // ---------- Check 1: Dead blocks (unreachable from StartBlock) ----------

    public static IEnumerable<WorkflowIssue> Check1_DeadBlocks(List<RouteBlock> blocks)
    {
        var startBlock = blocks.FirstOrDefault(b =>
            string.Equals(b.BlockType, "StartBlock", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(b.Name, "StartBlock", StringComparison.OrdinalIgnoreCase));

        if (startBlock == null)
        {
            yield return new WorkflowIssue(
                IssueSeverity.Warning,
                "DeadBlocks",
                "StartBlock не найден — невозможно определить достижимость блоков",
                null);
            yield break;
        }

        // BFS from StartBlock
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var queue = new Queue<string>();
        queue.Enqueue(startBlock.Guid);
        reachable.Add(startBlock.Guid);

        // Build guid -> block lookup
        var byGuid = blocks.ToDictionary(b => b.Guid, b => b, StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!byGuid.TryGetValue(current, out var block))
                continue;

            foreach (var connector in block.Connectors)
            {
                if (!string.IsNullOrEmpty(connector.ToBlock) && reachable.Add(connector.ToBlock))
                    queue.Enqueue(connector.ToBlock);
            }
        }

        foreach (var block in blocks)
        {
            if (!reachable.Contains(block.Guid))
            {
                var label = string.IsNullOrEmpty(block.Name) ? block.Guid : block.Name;
                yield return new WorkflowIssue(
                    IssueSeverity.Error,
                    "DeadBlocks",
                    $"Блок `{label}` (guid: {block.Guid}) недостижим из StartBlock",
                    block.Guid);
            }
        }
    }

    // ---------- Check 2: ConditionBlock without conditions ----------

    public static IEnumerable<WorkflowIssue> Check2_ConditionBlocksWithoutConditions(List<RouteBlock> blocks)
    {
        foreach (var block in blocks)
        {
            var isConditionBlock =
                string.Equals(block.BlockType, "ConditionBlock", StringComparison.OrdinalIgnoreCase) ||
                block.TypeString.Contains("ConditionBlock", StringComparison.OrdinalIgnoreCase);

            if (!isConditionBlock)
                continue;

            var hasAnyCondition = block.Connectors.Any(c => !string.IsNullOrWhiteSpace(c.Condition));

            if (!hasAnyCondition)
            {
                var label = string.IsNullOrEmpty(block.Name) ? block.Guid : block.Name;
                yield return new WorkflowIssue(
                    IssueSeverity.Warning,
                    "ConditionWithoutConditions",
                    $"ConditionBlock `{label}` не имеет условий в переходах (Connectors)",
                    block.Guid);
            }
        }
    }

    // ---------- Check 3: Handlers without code ----------

    public static IEnumerable<WorkflowIssue> Check3_HandlersWithoutCode(
        List<RouteBlock> blocks,
        string? handlerContent)
    {
        foreach (var block in blocks)
        {
            if (!block.GenerateHandler)
                continue;

            // If there is no handler file at all — report as warning
            if (string.IsNullOrEmpty(handlerContent))
            {
                var label = string.IsNullOrEmpty(block.Name) ? block.Guid : block.Name;
                yield return new WorkflowIssue(
                    IssueSeverity.Warning,
                    "HandlerWithoutCode",
                    $"Блок `{label}` имеет GenerateHandler:true, но файл *RouteHandlers.cs не найден рядом с .mtd",
                    block.Guid);
                continue;
            }

            // Look for a method named after the block
            if (!string.IsNullOrEmpty(block.Name) && !ContainsMethodName(handlerContent, block.Name))
            {
                yield return new WorkflowIssue(
                    IssueSeverity.Warning,
                    "HandlerWithoutCode",
                    $"Блок `{block.Name}` имеет GenerateHandler:true, но метод `{block.Name}` не найден в *RouteHandlers.cs",
                    block.Guid);
            }
        }
    }

    // ---------- Check 4: Empty blocks (dead ends, no outgoing transitions) ----------

    public static IEnumerable<WorkflowIssue> Check4_EmptyBlocks(List<RouteBlock> blocks)
    {
        foreach (var block in blocks)
        {
            // EndBlock is allowed to have no outgoing transitions
            var isEndBlock =
                string.Equals(block.BlockType, "EndBlock", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(block.Name, "EndBlock", StringComparison.OrdinalIgnoreCase) ||
                block.TypeString.Contains("EndBlock", StringComparison.OrdinalIgnoreCase);

            if (isEndBlock)
                continue;

            if (block.Connectors.Count == 0)
            {
                var label = string.IsNullOrEmpty(block.Name) ? block.Guid : block.Name;
                yield return new WorkflowIssue(
                    IssueSeverity.Warning,
                    "EmptyBlock",
                    $"Блок `{label}` не имеет исходящих переходов (тупик)",
                    block.Guid);
            }
        }
    }

    // ---------- Helpers ----------

    private static async Task<string?> LoadHandlerContent(string[] handlerFiles)
    {
        if (handlerFiles.Length == 0)
            return null;

        var sb = new StringBuilder();
        foreach (var file in handlerFiles)
        {
            try
            {
                sb.AppendLine(await File.ReadAllTextAsync(file));
            }
            catch
            {
                // skip unreadable
            }
        }
        return sb.Length > 0 ? sb.ToString() : null;
    }

    private static bool ContainsMethodName(string content, string methodName)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(methodName))
            return false;
        return Regex.IsMatch(content, @"\b" + Regex.Escape(methodName) + @"\s*\(");
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    // ---------- Report formatting ----------

    private static string FormatReport(
        string path,
        int totalMtdFiles,
        List<WorkflowFileResult> results,
        string severityFilter)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Валидация маршрутных схем Directum RX");
        sb.AppendLine();
        sb.AppendLine($"**Путь**: `{path}`");
        sb.AppendLine($"**MTD файлов проверено**: {totalMtdFiles}");
        sb.AppendLine($"**Файлов с RouteScheme**: {results.Sum(r => 1)}");

        var severityDisplay = severityFilter switch
        {
            "error" => "только ошибки",
            "warning" => "только предупреждения",
            _ => "все"
        };
        sb.AppendLine($"**Фильтр серьёзности**: {severityDisplay}");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("**Результат**: Проблем не обнаружено.");
            return sb.ToString();
        }

        var totalIssues = results.Sum(r => r.Issues.Count);
        var errorCount = results.Sum(r => r.Issues.Count(i => i.Severity == IssueSeverity.Error));
        var warningCount = results.Sum(r => r.Issues.Count(i => i.Severity == IssueSeverity.Warning));

        sb.AppendLine($"**Итого**: {errorCount} ошибок, {warningCount} предупреждений");
        sb.AppendLine();

        foreach (var fileResult in results)
        {
            sb.AppendLine($"## `{Path.GetFileName(fileResult.FilePath)}`");
            sb.AppendLine($"Путь: `{fileResult.FilePath}`");
            sb.AppendLine($"Блоков: {fileResult.BlockCount}");
            sb.AppendLine();

            var errors = fileResult.Issues.Where(i => i.Severity == IssueSeverity.Error).ToList();
            var warnings = fileResult.Issues.Where(i => i.Severity == IssueSeverity.Warning).ToList();

            if (errors.Count > 0)
            {
                sb.AppendLine("### Ошибки");
                sb.AppendLine();
                foreach (var issue in errors)
                    sb.AppendLine($"- **[{issue.CheckName}]** {issue.Message}");
                sb.AppendLine();
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine("### Предупреждения");
                sb.AppendLine();
                foreach (var issue in warnings)
                    sb.AppendLine($"- **[{issue.CheckName}]** {issue.Message}");
                sb.AppendLine();
            }
        }

        // Collect which check names actually appeared in the report
        var presentCheckNames = results
            .SelectMany(r => r.Issues)
            .Select(i => i.CheckName)
            .ToHashSet(StringComparer.Ordinal);

        if (presentCheckNames.Count > 0)
        {
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("## Как исправить");
            sb.AppendLine();

            var fixes = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DeadBlocks"] = "Соедините блок с остальной схемой или удалите его.",
                ["ConditionWithoutConditions"] = "Добавьте условие хотя бы в один Connector блока.",
                ["HandlerWithoutCode"] = "Добавьте метод с соответствующим именем в *RouteHandlers.cs.",
                ["EmptyBlock"] = "Добавьте исходящий переход из блока или пометьте его как EndBlock."
            };

            foreach (var (checkName, hint) in fixes)
            {
                if (presentCheckNames.Contains(checkName))
                    sb.AppendLine($"- **{checkName}**: {hint}");
            }
        }

        return sb.ToString();
    }

    // ---------- Data types ----------

    public record RouteConnector(string ToBlock, string Condition);

    public record RouteBlock(
        string Guid,
        string Name,
        string BlockType,
        string TypeString,
        bool GenerateHandler,
        List<RouteConnector> Connectors);

    public record WorkflowIssue(
        IssueSeverity Severity,
        string CheckName,
        string Message,
        string? BlockGuid);

    private record WorkflowFileResult(
        string FilePath,
        int BlockCount,
        List<WorkflowIssue> Issues);

    public enum IssueSeverity { Error, Warning }
}
