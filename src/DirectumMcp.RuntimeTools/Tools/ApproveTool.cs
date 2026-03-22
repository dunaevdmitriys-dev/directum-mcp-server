using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class ApproveTool
{
    private readonly DirectumODataClient _client;
    public ApproveTool(DirectumODataClient client) => _client = client;

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
            // 1. Check assignment
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

            // 2. Validate result
            var validResults = new[] { "Approved", "ForRevision", "Rejected", "Signed", "ForReapproval" };
            if (!validResults.Contains(result, StringComparer.OrdinalIgnoreCase))
                return $"Недопустимый результат `{result}`. Допустимые: {string.Join(", ", validResults)}";

            // 3. Execute
            var body = new Dictionary<string, object> { ["Result"] = result };
            if (!string.IsNullOrWhiteSpace(comment))
                body["ActiveText"] = comment;

            await _client.PostActionAsync("IAssignments", assignmentId, "Complete",
                System.Text.Json.JsonSerializer.Serialize(body));

            // 4. Report
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
            if (!string.IsNullOrEmpty(taskSubject))
                sb.AppendLine($"Задача: {taskSubject}");
            if (!string.IsNullOrWhiteSpace(comment))
                sb.AppendLine($"Комментарий: {comment}");
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
}
