using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class MyAssignmentsTool
{
    private readonly DirectumODataClient _client;

    public MyAssignmentsTool(DirectumODataClient client)
    {
        _client = client;
    }

    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "InProcess", "Completed", "Aborted"
    };

    [McpServerTool(Name = "my_tasks")]
    [Description("Мои задания в Directum RX — активные, просроченные, выполненные")]
    public async Task<string> GetMyAssignments(
        [Description("Статус заданий: InProcess, Completed, Aborted")] string status = "InProcess",
        [Description("Максимальное количество результатов")] int top = 20)
    {
        top = Math.Clamp(top, 1, 100);
        try
        {
            // Validate status against allowlist
            if (!AllowedStatuses.Contains(status))
                return $"Ошибка: недопустимый статус '{status}'. Допустимые значения: {string.Join(", ", AllowedStatuses)}.";

            var filter = $"Status eq '{EscapeOData(status)}'";
            var select = "Id,Subject,Author,Deadline,Created,Status,Importance";

            var result = await _client.GetAsync(
                "IAssignments",
                filter: filter,
                select: select,
                orderby: "Deadline asc",
                top: top);

            return FormatResults(result, status);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить задания. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static string FormatResults(JsonElement result, string status)
    {
        var items = GetItems(result);
        if (items.Count == 0)
            return $"Задания со статусом '{status}' не найдены.";

        var today = DateTime.UtcNow.Date;
        var overdueCount = 0;

        var sb = new StringBuilder();
        sb.AppendLine($"Задания ({status}): {items.Count}");
        sb.AppendLine();
        sb.AppendLine("| | ID | Тема | Автор | Срок | Создано | Важность |");
        sb.AppendLine("|---|---|---|---|---|---|---|");

        foreach (var item in items)
        {
            var id = GetString(item, "Id");
            var subject = GetString(item, "Subject");
            var author = GetNestedString(item, "Author", "Name");
            var deadlineStr = GetString(item, "Deadline");
            var created = FormatDate(GetString(item, "Created"), "dd.MM.yyyy HH:mm");
            var importance = GetString(item, "Importance");

            var isOverdue = false;
            var deadlineFormatted = FormatDate(deadlineStr, "dd.MM.yyyy HH:mm");
            if (deadlineStr != "-" && DateTime.TryParse(deadlineStr, out var deadline))
            {
                isOverdue = deadline.Date < today && status == "InProcess";
                if (isOverdue)
                    overdueCount++;
            }

            var statusIcon = isOverdue ? "!!!" : (importance == "High" ? "!" : " ");

            sb.AppendLine($"| {statusIcon} | {id} | {subject} | {author} | {deadlineFormatted} | {created} | {importance} |");
        }

        if (overdueCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine($"**ПРОСРОЧЕНО: {overdueCount} из {items.Count} заданий** (отмечены `!!!`)");
        }

        sb.AppendLine();
        sb.AppendLine("*Для выполнения задания: `complete assignmentId=<ID>`*");
        sb.AppendLine("*Для массового выполнения: `bulk_complete`*");

        return sb.ToString();
    }
}
