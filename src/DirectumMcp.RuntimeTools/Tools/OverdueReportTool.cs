using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class OverdueReportTool
{
    private readonly DirectumODataClient _client;

    public OverdueReportTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "overdue_report")]
    [Description("Отчёт по просроченным заданиям — группировка по исполнителям, сортировка по длительности просрочки.")]
    public async Task<string> OverdueReport(
        [Description("Максимум результатов")] int top = 50,
        [Description("Группировать по: performer | importance | author")] string groupBy = "performer")
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
            var filter = $"Status eq 'InProcess' and Deadline lt {now}";
            var select = "Id,Subject,Deadline,Created,Importance";

            var result = await _client.GetAsync(
                "IAssignments",
                filter: filter,
                select: select,
                orderby: "Deadline asc",
                top: top,
                expand: "Performer,Author");

            var items = GetItems(result);
            var overdueItems = ParseOverdueItems(items);

            return FormatReport(overdueItems, groupBy);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить просроченные задания. Проверьте переменные RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static List<OverdueItem> ParseOverdueItems(List<JsonElement> items)
    {
        var now = DateTime.UtcNow;
        var result = new List<OverdueItem>();
        foreach (var item in items)
        {
            var id = GetLong(item, "Id");
            var subject = GetString(item, "Subject");
            var performer = GetNestedString(item, "Performer", "Name");
            var author = GetNestedString(item, "Author", "Name");
            var importance = GetString(item, "Importance");
            var deadlineStr = GetString(item, "Deadline");

            if (!DateTime.TryParse(deadlineStr, out var deadline))
                continue;

            var overdueDays = (now - deadline).TotalDays;
            result.Add(new OverdueItem(id, subject, performer, author, importance, deadline, overdueDays));
        }
        return result;
    }

    internal static string FormatReport(List<OverdueItem> items, string groupBy)
    {
        var today = DateTime.UtcNow.Date;
        var sb = new StringBuilder();
        sb.AppendLine("# Отчёт по просроченным заданиям");
        sb.AppendLine();
        sb.AppendLine($"**Дата:** {today:dd.MM.yyyy}");
        sb.AppendLine($"**Всего просрочено:** {items.Count}");

        if (items.Count == 0)
        {
            sb.AppendLine();
            sb.AppendLine("Просроченных заданий не найдено.");
            return sb.ToString();
        }

        var groupTitle = groupBy.ToLowerInvariant() switch
        {
            "importance" => "важности",
            "author" => "авторам",
            _ => "исполнителям"
        };

        sb.AppendLine();
        sb.AppendLine($"## По {groupTitle}");

        Func<OverdueItem, string> groupSelector = groupBy.ToLowerInvariant() switch
        {
            "importance" => item => item.Importance,
            "author" => item => item.Author,
            _ => item => item.Performer
        };

        var groups = items
            .GroupBy(groupSelector)
            .OrderByDescending(g => g.Count());

        foreach (var group in groups)
        {
            sb.AppendLine();
            sb.AppendLine($"### {group.Key} ({group.Count()} заданий)");
            sb.AppendLine();
            sb.AppendLine("| ID | Тема | Срок | Просрочка (дн) | Автор | Важность |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var item in group.OrderBy(i => i.Deadline))
            {
                var deadlineFormatted = item.Deadline.ToString("dd.MM.yyyy HH:mm");
                sb.AppendLine($"| {item.Id} | {item.Subject} | {deadlineFormatted} | {item.OverdueDays:F1} | {item.Author} | {item.Importance} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("*Для выполнения задания: `complete assignmentId=<ID>`*");

        return sb.ToString();
    }
}

internal record OverdueItem(long Id, string Subject, string Performer, string Author, string Importance, DateTime Deadline, double OverdueDays);
