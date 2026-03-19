using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class PendingApprovalsTool
{
    private readonly DirectumODataClient _client;

    public PendingApprovalsTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "pending_approvals")]
    [Description("Документы, ожидающие согласования/подписания текущим пользователем. Показывает тип документа, автора, срок, сколько дней ожидает.")]
    public async Task<string> GetPendingApprovals(
        [Description("Максимум результатов")] int top = 30,
        [Description("Сортировка: deadline (по дедлайну) или waiting (по времени ожидания)")] string sort = "deadline")
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            // Get approval assignments (InProcess) for current user
            var filter = "Status eq 'InProcess'";
            var result = await _client.GetAsync(
                "IApprovalAssignments",
                filter: filter,
                select: "Id,Subject,Created,Deadline,Importance,Result",
                expand: "Author,MainTask",
                top: top * 2); // Get extra to account for filtering

            var items = GetItems(result);

            if (items.Count == 0)
            {
                // Fallback: try IAssignments with approval-related subjects
                var fallbackFilter = "Status eq 'InProcess' and (contains(Subject, 'Согласование') or contains(Subject, 'Подписание') or contains(Subject, 'Рассмотрение'))";
                result = await _client.GetAsync(
                    "IAssignments",
                    filter: fallbackFilter,
                    select: "Id,Subject,Created,Deadline,Importance",
                    expand: "Author",
                    top: top);
                items = GetItems(result);
            }

            return FormatReport(items, top, sort);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: {ex.Message}\nПроверьте RX_ODATA_URL, RX_USERNAME, RX_PASSWORD.";
        }
    }

    internal static string FormatReport(List<JsonElement> items, int top, string sort)
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

        // Sort
        parsed = sort.ToLowerInvariant() switch
        {
            "waiting" => parsed.OrderByDescending(a => a.WaitingHours).ToList(),
            _ => parsed.OrderBy(a => a.Deadline ?? DateTime.MaxValue).ToList()
        };

        var display = parsed.Take(top).ToList();

        // Summary
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

            var waitingDisplay = a.WaitingHours < 24
                ? $"{a.WaitingHours:F0}ч"
                : $"{a.WaitingHours / 24:F1}д";

            var status = a.IsOverdue ? "🔴 Просрочено" :
                a.Importance == "High" ? "⚠️ Важное" : "⏳";

            sb.AppendLine($"| {i + 1} | {subj} | {auth} | {deadlineDisplay} | {waitingDisplay} | {status} |");
        }

        if (overdueCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"> ⚠️ **{overdueCount}** документ(ов) просрочены. Используйте `complete` для выполнения заданий.");
        }

        return sb.ToString();
    }
}

internal record ApprovalItem(
    long Id, string Subject, string Author, DateTime Created,
    DateTime? Deadline, double WaitingHours, bool IsOverdue, string Importance);
