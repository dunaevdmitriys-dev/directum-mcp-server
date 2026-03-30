using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.Runtime.Tools;

[McpServerToolType]
public class ApprovalTools
{
    private readonly DirectumODataClient _client;
    public ApprovalTools(DirectumODataClient client) => _client = client;

    #region ApproveTool

    [McpServerTool(Name = "approve")]
    [Description("Согласовать или отклонить документ. Результат: Approved (согласовано), ForRevision (на доработку), Rejected (отклонено), Signed (подписано).")]
    public async Task<string> Approve(
        [Description("ID задания на согласование")] long assignmentId,
        [Description("Результат: Approved, ForRevision, Rejected, Signed")] string result = "Approved",
        [Description("Комментарий к решению")] string comment = "")
    {
        var sb = new StringBuilder();
        try
        {
            var assignmentJson = await _client.GetAsync("IAssignments",
                $"Id eq {assignmentId}", "Id,Subject,Status",
                expand: "Performer($select=Id,Name),Task($select=Id,Subject)");

            if (!assignmentJson.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
                return $"Задание #{assignmentId} не найдено.";

            var item = values[0];
            var status = item.TryGetProperty("Status", out var s) ? s.GetString() ?? "" : "";
            if (status != "InProcess")
                return $"Задание #{assignmentId} имеет статус `{status}`. Согласование возможно только для `InProcess`.";

            var subject = item.TryGetProperty("Subject", out var subj) ? subj.GetString() ?? "" : "";
            var taskSubject = "";
            if (item.TryGetProperty("Task", out var task) && task.ValueKind == JsonValueKind.Object)
                taskSubject = task.TryGetProperty("Subject", out var ts) ? ts.GetString() ?? "" : "";

            var validResults = new[] { "Approved", "ForRevision", "Rejected", "Signed", "ForReapproval" };
            if (!validResults.Contains(result, StringComparer.OrdinalIgnoreCase))
                return $"Недопустимый результат `{result}`. Допустимые: {string.Join(", ", validResults)}";

            var body = new Dictionary<string, object> { ["Result"] = result };
            if (!string.IsNullOrWhiteSpace(comment)) body["ActiveText"] = comment;

            await _client.PostActionAsync("IAssignments", assignmentId, "Complete",
                JsonSerializer.Serialize(body));

            var resultRu = result switch
            {
                "Approved" => "Согласовано",
                "ForRevision" or "ForReapproval" => "На доработку",
                "Rejected" => "Отклонено",
                "Signed" => "Подписано",
                _ => result
            };

            sb.AppendLine($"Задание #{assignmentId} — {resultRu}");
            sb.AppendLine();
            sb.AppendLine($"Тема: {subject}");
            if (!string.IsNullOrEmpty(taskSubject)) sb.AppendLine($"Задача: {taskSubject}");
            if (!string.IsNullOrWhiteSpace(comment)) sb.AppendLine($"Комментарий: {comment}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка согласования: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Возможные причины:");
            sb.AppendLine("- Нет прав на выполнение этого задания");
            sb.AppendLine("- Задание уже выполнено другим пользователем");
            sb.AppendLine("- Тип задания не поддерживает этот результат");
        }
        return sb.ToString();
    }

    #endregion

    #region PendingApprovalsTool

    [McpServerTool(Name = "pending_approvals")]
    [Description("Документы, ожидающие согласования/подписания текущим пользователем. Показывает тип документа, автора, срок, сколько дней ожидает.")]
    public async Task<string> GetPendingApprovals(
        [Description("Максимум результатов")] int top = 30,
        [Description("Сортировка: deadline (по дедлайну) или waiting (по времени ожидания)")] string sort = "deadline")
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            var filter = "Status eq 'InProcess'";
            var result = await _client.GetAsync("IApprovalAssignments",
                filter: filter,
                select: "Id,Subject,Created,Deadline,Importance,Result",
                expand: "Author,MainTask",
                top: top * 2);

            var items = GetItems(result);

            if (items.Count == 0)
            {
                var fallbackFilter = "Status eq 'InProcess' and (contains(Subject, 'Согласование') or contains(Subject, 'Подписание') or contains(Subject, 'Рассмотрение'))";
                result = await _client.GetAsync("IAssignments",
                    filter: fallbackFilter,
                    select: "Id,Subject,Created,Deadline,Importance",
                    expand: "Author",
                    top: top);
                items = GetItems(result);
            }

            return FormatPendingReport(items, top, sort);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}\nПроверьте RX_ODATA_URL, RX_USERNAME, RX_PASSWORD.";
        }
    }

    private static string FormatPendingReport(List<JsonElement> items, int top, string sort)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Документы на согласовании");
        sb.AppendLine();

        if (items.Count == 0)
        {
            sb.AppendLine("Нет документов, ожидающих вашего согласования/подписания.");
            return sb.ToString();
        }

        var now = DateTime.UtcNow;
        var parsed = new List<ApprovalItem>();

        foreach (var item in items)
        {
            var id = GetLong(item, "Id");
            var subject = GetString(item, "Subject");
            var createdStr = GetString(item, "Created");
            var deadlineStr = GetString(item, "Deadline");
            var importance = GetString(item, "Importance");
            var author = GetNestedString(item, "Author", "Name");

            DateTime.TryParse(createdStr, out var created);
            DateTime? deadline = DateTime.TryParse(deadlineStr, out var dl) ? dl : null;
            var waitingHours = (now - created).TotalHours;
            var isOverdue = deadline.HasValue && now > deadline.Value;

            parsed.Add(new ApprovalItem(id, subject, author, created, deadline, waitingHours, isOverdue, importance));
        }

        parsed = sort.ToLowerInvariant() switch
        {
            "waiting" => parsed.OrderByDescending(a => a.WaitingHours).ToList(),
            _ => parsed.OrderBy(a => a.Deadline ?? DateTime.MaxValue).ToList()
        };

        var display = parsed.Take(top).ToList();
        var overdueCount = parsed.Count(a => a.IsOverdue);
        var highCount = parsed.Count(a => a.Importance == "High");

        sb.AppendLine($"**Всего:** {parsed.Count} | **Просрочено:** {overdueCount} | **Важных:** {highCount}");
        sb.AppendLine();
        sb.AppendLine("| # | Документ | Автор | Дедлайн | Ожидает | Статус |");
        sb.AppendLine("|---|---|---|---|---|---|");

        for (var i = 0; i < display.Count; i++)
        {
            var a = display[i];
            var subj = a.Subject.Length > 45 ? a.Subject[..42] + "..." : a.Subject;
            var auth = a.Author.Length > 20 ? a.Author[..17] + "..." : a.Author;
            var deadlineDisplay = a.Deadline.HasValue ? a.Deadline.Value.ToString("dd.MM HH:mm") : "-";
            var waitingDisplay = a.WaitingHours < 24 ? $"{a.WaitingHours:F0}ч" : $"{a.WaitingHours / 24:F1}д";
            var statusText = a.IsOverdue ? "Просрочено" : a.Importance == "High" ? "Важное" : "-";
            sb.AppendLine($"| {i + 1} | {subj} | {auth} | {deadlineDisplay} | {waitingDisplay} | {statusText} |");
        }

        if (overdueCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"> **{overdueCount}** документ(ов) просрочены. Используйте `complete` для выполнения заданий.");
        }
        return sb.ToString();
    }

    private record ApprovalItem(
        long Id, string Subject, string Author, DateTime Created,
        DateTime? Deadline, double WaitingHours, bool IsOverdue, string Importance);

    #endregion

    #region BulkCompleteTool

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
            var filters = new List<string> { "Status eq 'InProcess'" };

            if (string.Equals(taskType, "Acquaintance", StringComparison.OrdinalIgnoreCase))
                filters.Add("(contains(Subject, 'ознакомлен') or contains(Subject, 'Acquaintance'))");
            else if (string.Equals(taskType, "Approval", StringComparison.OrdinalIgnoreCase))
                filters.Add("(contains(Subject, 'согласован') or contains(Subject, 'Approval'))");

            var data = await _client.GetAsync("IAssignments",
                filter: string.Join(" and ", filters),
                select: "Id,Subject,Author,Deadline,Created",
                orderby: "Deadline asc",
                top: limit);

            var items = GetItems(data);
            if (items.Count == 0) return "Задания не найдены.";

            if (!confirmed) return FormatBulkPreview(items, taskType, result, comment);
            return await ExecuteBulkComplete(items, result, comment);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось выполнить массовое завершение заданий. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static string FormatBulkPreview(List<JsonElement> items, string taskType, string result, string? comment)
    {
        var today = DateTime.UtcNow.Date;
        var sb = new StringBuilder();
        sb.AppendLine("## Предпросмотр массового выполнения");
        sb.AppendLine();
        sb.AppendLine($"Найдено заданий: {items.Count}");
        sb.AppendLine($"Тип: {taskType}");
        sb.AppendLine($"Результат: {result}");
        if (!string.IsNullOrWhiteSpace(comment)) sb.AppendLine($"Комментарий: {comment}");
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
                var current = await _client.GetByIdAsync("IAssignments", id, select: "Status");
                var currentStatus = GetString(current, "Status");
                if (currentStatus != "InProcess")
                {
                    skipped++;
                    details.Add((i + 1, id.ToString(), subject, $"Пропущено ({currentStatus})"));
                    continue;
                }

                var actionBody = new Dictionary<string, object?> { ["Result"] = result };
                if (!string.IsNullOrWhiteSpace(comment)) actionBody["ActiveText"] = comment;
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
        if (skipped > 0) sb.AppendLine($"| Пропущено (статус изменился) | {skipped} |");
        if (errors > 0) sb.AppendLine($"| Ошибка | {errors} |");
        sb.AppendLine();
        sb.AppendLine("### Детали");
        sb.AppendLine("| # | ID | Тема | Статус |");
        sb.AppendLine("|---|-----|------|--------|");
        foreach (var (index, id, subject, status) in details)
            sb.AppendLine($"| {index} | {id} | {subject} | {status} |");
        return sb.ToString();
    }

    #endregion
}
