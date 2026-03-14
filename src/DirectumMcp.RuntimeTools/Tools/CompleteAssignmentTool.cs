using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class CompleteAssignmentTool
{
    private readonly DirectumODataClient _client;

    public CompleteAssignmentTool(DirectumODataClient client)
    {
        _client = client;
    }

    [McpServerTool(Name = "complete")]
    [Description("Выполнить задание в Directum RX")]
    public async Task<string> Complete(
        [Description("ID задания")] long assignmentId,
        [Description("Результат выполнения: Complete, ForRevision, Abort, Explored и др.")] string result = "Complete",
        [Description("Текст комментария при выполнении")] string? activeText = null)
    {
        try
        {
            // First, get the assignment to verify it exists and show details
            var assignment = await _client.GetByIdAsync("IAssignments", assignmentId);

            var currentStatus = GetString(assignment, "Status");
            if (currentStatus != "InProcess")
            {
                return $"Задание {assignmentId} нельзя выполнить: текущий статус '{currentStatus}' (ожидается 'InProcess').";
            }

            var subject = GetString(assignment, "Subject");

            // Build action body
            var actionBody = new Dictionary<string, object?>
            {
                ["Result"] = result
            };

            if (!string.IsNullOrWhiteSpace(activeText))
                actionBody["ActiveText"] = activeText;

            // Execute the Complete action via OData
            var response = await _client.PostActionAsync("IAssignments", assignmentId, "Complete", actionBody);

            var sb = new StringBuilder();
            sb.AppendLine("Задание выполнено.");
            sb.AppendLine();
            sb.AppendLine($"- **ID**: {assignmentId}");
            sb.AppendLine($"- **Тема**: {subject}");
            sb.AppendLine($"- **Результат**: {result}");
            if (!string.IsNullOrWhiteSpace(activeText))
                sb.AppendLine($"- **Комментарий**: {activeText}");

            return sb.ToString();
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось выполнить задание {assignmentId}. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }
}
