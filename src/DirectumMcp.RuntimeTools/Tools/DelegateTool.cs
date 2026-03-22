using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class DelegateTool
{
    private readonly DirectumODataClient _client;
    public DelegateTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "delegate")]
    [Description("Переадресовать задание другому сотруднику. Проверяет статус задания перед переадресацией.")]
    public async Task<string> Delegate(
        [Description("ID задания для переадресации")] long assignmentId,
        [Description("ID сотрудника, кому переадресовать")] long delegateToId,
        [Description("Комментарий к переадресации")] string comment = "")
    {
        var sb = new StringBuilder();

        try
        {
            // 1. Check assignment exists and is InProcess
            var assignment = await _client.GetAsync("IAssignments",
                $"$filter=Id eq {assignmentId}&$select=Id,Subject,Status&$expand=Performer($select=Id,Name)");

            if (assignment.ValueKind == JsonValueKind.Undefined)
                return $"**ОШИБКА**: Не удалось получить задание {assignmentId}.";

            var values = assignment.GetProperty("value");
            if (values.GetArrayLength() == 0)
                return $"**ОШИБКА**: Задание {assignmentId} не найдено.";

            var item = values[0];
            var status = item.TryGetProperty("Status", out var s) ? s.GetString() : "";
            if (status != "InProcess")
                return $"**ОШИБКА**: Задание {assignmentId} имеет статус `{status}`. Переадресация возможна только для `InProcess`.";

            var subject = item.TryGetProperty("Subject", out var subj) ? subj.GetString() ?? "" : "";
            var currentPerformer = "?";
            if (item.TryGetProperty("Performer", out var perf) && perf.TryGetProperty("Name", out var pn))
                currentPerformer = pn.GetString() ?? "?";

            // 2. Check target employee
            var employee = await _client.GetAsync("IEmployees",
                $"$filter=Id eq {delegateToId}&$select=Id,Name,Status");

            if (employee.ValueKind == JsonValueKind.Undefined)
                return $"**ОШИБКА**: Не удалось проверить сотрудника {delegateToId}.";

            var empValues = employee.GetProperty("value");
            if (empValues.GetArrayLength() == 0)
                return $"**ОШИБКА**: Сотрудник {delegateToId} не найден.";

            var empName = empValues[0].TryGetProperty("Name", out var en) ? en.GetString() ?? "?" : "?";
            var empStatus = empValues[0].TryGetProperty("Status", out var es) ? es.GetString() : "";
            if (empStatus == "Closed")
                return $"**ОШИБКА**: Сотрудник `{empName}` закрыт (Status=Closed). Переадресация невозможна.";

            // 3. Execute Forward action
            var body = new { ForwardTo = new { Id = delegateToId } };
            if (!string.IsNullOrWhiteSpace(comment))
                body = new { ForwardTo = new { Id = delegateToId } };

            var result = await _client.PostActionAsync("IAssignments", assignmentId, "Forward",
                JsonSerializer.Serialize(body));

            sb.AppendLine("## Задание переадресовано");
            sb.AppendLine();
            sb.AppendLine($"**Задание:** #{assignmentId} — {subject}");
            sb.AppendLine($"**От:** {currentPerformer}");
            sb.AppendLine($"**Кому:** {empName} (ID: {delegateToId})");
            if (!string.IsNullOrWhiteSpace(comment))
                sb.AppendLine($"**Комментарий:** {comment}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка переадресации:** {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Возможные причины:");
            sb.AppendLine("- Нет прав на переадресацию");
            sb.AppendLine("- Задание уже выполнено/отменено");
            sb.AppendLine("- OData Action 'Forward' не поддерживается для этого типа задания");
        }

        return sb.ToString();
    }
}
