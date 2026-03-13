using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class BulkCompleteTool
{
    private readonly DirectumODataClient _client;

    public BulkCompleteTool(DirectumODataClient client)
    {
        _client = client;
    }

    private static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Acquaintance", "Approval", "All"
    };

    [McpServerTool(Name = "bulk_complete")]
    [Description("Массовое выполнение заданий в Directum RX. По умолчанию — только предпросмотр (confirmed=false).")]
    public async Task<string> BulkComplete(
        [Description("Тип: Acquaintance (ознакомление), Approval (согласование), All")] string taskType = "All",
        [Description("Результат выполнения")] string result = "Complete",
        [Description("Комментарий к выполнению")] string? comment = null,
        [Description("Максимум заданий (1-100)")] int limit = 50,
        [Description("true = выполнить, false = только показать список")] bool confirmed = false)
    {
        try
        {
            if (!ValidTypes.Contains(taskType))
                return $"Недопустимый тип: {taskType}. Допустимые: Acquaintance, Approval, All";

            limit = Math.Clamp(limit, 1, 100);

            // Build OData filter
            var filters = new List<string> { "Status eq 'InProcess'" };

            if (string.Equals(taskType, "Acquaintance", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("(contains(Subject, 'ознакомлен') or contains(Subject, 'Acquaintance'))");
            }
            else if (string.Equals(taskType, "Approval", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add("(contains(Subject, 'согласован') or contains(Subject, 'Approval'))");
            }

            var data = await _client.GetAsync(
                "IAssignments",
                filter: string.Join(" and ", filters),
                select: "Id,Subject,Author,Deadline,Created",
                orderby: "Deadline asc",
                top: limit);

            var items = GetItems(data);

            if (items.Count == 0)
                return "Задания не найдены.";

            if (!confirmed)
                return FormatPreview(items, taskType, result, comment);

            return await ExecuteBulkComplete(items, result, comment);
        }
        catch (Exception ex)
        {
            return $"Ошибка при массовом выполнении: {ex.Message}";
        }
    }

    private static string FormatPreview(List<JsonElement> items, string taskType, string result, string? comment)
    {
        var today = DateTime.UtcNow.Date;
        var sb = new StringBuilder();

        sb.AppendLine("## Предпросмотр массового выполнения");
        sb.AppendLine();
        sb.AppendLine($"Найдено заданий: {items.Count}");
        sb.AppendLine($"Тип: {taskType}");
        sb.AppendLine($"Результат: {result}");
        if (!string.IsNullOrWhiteSpace(comment))
            sb.AppendLine($"Комментарий: {comment}");
        sb.AppendLine();

        sb.AppendLine("| # | ID | Тема | Автор | Срок | Просрочено |");
        sb.AppendLine("|---|-----|------|-------|------|-----------|");

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var id = GetString(item, "Id");
            var subject = GetString(item, "Subject");
            var author = GetNestedString(item, "Author", "Name");
            var deadlineStr = GetString(item, "Deadline");
            var deadlineFormatted = FormatDate(deadlineStr, "dd.MM.yyyy HH:mm");

            var isOverdue = false;
            if (deadlineStr != "-" && DateTime.TryParse(deadlineStr, out var deadline))
                isOverdue = deadline.Date < today;

            sb.AppendLine($"| {i + 1} | {id} | {subject} | {author} | {deadlineFormatted} | {(isOverdue ? "Да" : "Нет")} |");
        }

        sb.AppendLine();
        sb.AppendLine("> Для выполнения вызовите `bulk_complete` с `confirmed=true`");

        return sb.ToString();
    }

    private async Task<string> ExecuteBulkComplete(List<JsonElement> items, string result, string? comment)
    {
        var completed = 0;
        var skipped = 0;
        var errors = 0;
        var details = new List<(int Index, string Id, string Subject, string Status)>();

        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var id = GetLong(item, "Id");
            var subject = GetString(item, "Subject");

            try
            {
                // Re-check status before completing
                var current = await _client.GetByIdAsync("IAssignments", id, select: "Status");
                var currentStatus = GetString(current, "Status");

                if (currentStatus != "InProcess")
                {
                    skipped++;
                    details.Add((i + 1, id.ToString(), subject, $"Пропущено ({currentStatus})"));
                    continue;
                }

                var actionBody = new Dictionary<string, object?> { ["Result"] = result };
                if (!string.IsNullOrWhiteSpace(comment))
                    actionBody["ActiveText"] = comment;

                await _client.PostActionAsync("IAssignments", id, "Complete", actionBody);

                completed++;
                details.Add((i + 1, id.ToString(), subject, "Выполнено"));
            }
            catch (Exception ex)
            {
                errors++;
                details.Add((i + 1, id.ToString(), subject, $"Ошибка: {ex.Message}"));
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("## Результат массового выполнения");
        sb.AppendLine();
        sb.AppendLine("| Результат | Количество |");
        sb.AppendLine("|-----------|-----------|");
        sb.AppendLine($"| Выполнено | {completed} |");
        if (skipped > 0)
            sb.AppendLine($"| Пропущено (статус изменился) | {skipped} |");
        if (errors > 0)
            sb.AppendLine($"| Ошибка | {errors} |");
        sb.AppendLine();

        sb.AppendLine("### Детали");
        sb.AppendLine("| # | ID | Тема | Статус |");
        sb.AppendLine("|---|-----|------|--------|");

        foreach (var (index, id, subject, status) in details)
        {
            sb.AppendLine($"| {index} | {id} | {subject} | {status} |");
        }

        return sb.ToString();
    }
}
