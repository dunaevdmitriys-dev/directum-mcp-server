using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class CreateTaskTool
{
    private readonly DirectumODataClient _client;

    public CreateTaskTool(DirectumODataClient client)
    {
        _client = client;
    }

    private static readonly HashSet<string> AllowedImportance = new(StringComparer.OrdinalIgnoreCase)
    {
        "Low", "Normal", "High"
    };

    [McpServerTool(Name = "send_task")]
    [Description("Создать простую задачу в Directum RX")]
    public async Task<string> Create(
        [Description("Тема задачи")] string subject,
        [Description("Имя исполнителя (для поиска среди сотрудников)")] string? assigneeName = null,
        [Description("Срок выполнения (yyyy-MM-dd или yyyy-MM-ddTHH:mm:ss)")] string? deadline = null,
        [Description("Текст задачи")] string? description = null,
        [Description("Важность: Low, Normal, High")] string importance = "Normal",
        [Description("Автоматически стартовать задачу после создания")] bool autoStart = false)
    {
        try
        {
            // Validate importance against allowlist
            if (!AllowedImportance.Contains(importance))
                return $"Ошибка: недопустимая важность '{importance}'. Допустимые значения: {string.Join(", ", AllowedImportance)}.";

            // Validate deadline format
            if (!string.IsNullOrWhiteSpace(deadline) &&
                !DateTime.TryParseExact(deadline, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
                !DateTime.TryParseExact(deadline, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return "Ошибка: параметр deadline должен быть в формате yyyy-MM-dd или yyyy-MM-ddTHH:mm:ss.";

            // Lookup assignee if name provided
            long? assigneeId = null;
            string? assigneeFullName = null;

            if (!string.IsNullOrWhiteSpace(assigneeName))
            {
                var (id, name) = await LookupEmployeeAsync(assigneeName);
                if (id == null)
                    return $"Сотрудник '{assigneeName}' не найден. Уточните имя и попробуйте снова.";

                assigneeId = id;
                assigneeFullName = name;
            }

            // Build task body
            var taskBody = new Dictionary<string, object?>
            {
                ["Subject"] = subject,
                ["Importance"] = importance
            };

            if (!string.IsNullOrWhiteSpace(description))
                taskBody["ActiveText"] = description;

            if (!string.IsNullOrWhiteSpace(deadline))
            {
                // Normalize deadline to include time if missing
                if (!deadline.Contains('T'))
                    deadline += "T23:59:59Z";
                else if (!deadline.EndsWith('Z') && !deadline.Contains('+'))
                    deadline += "Z";

                taskBody["MaxDeadline"] = deadline;
            }

            // Create the task
            var createdTask = await _client.PostAsync("ISimpleTasks", taskBody);
            var taskId = GetLong(createdTask, "Id");

            if (taskId == 0)
                return "Ошибка: задача создана, но не удалось получить ID.";

            // Add assignee as route step if provided
            if (assigneeId != null)
            {
                try
                {
                    var routeStep = new Dictionary<string, object?>
                    {
                        ["Performer"] = new { Id = assigneeId.Value },
                        ["Deadline"] = deadline
                    };

                    await _client.PostActionAsync("ISimpleTasks", taskId, "RouteSteps", routeStep);
                }
                catch (Exception ex)
                {
                    // Task created but route step failed — report partial success
                    var partial = new StringBuilder();
                    partial.AppendLine($"Задача создана (ID: {taskId}), но не удалось назначить исполнителя: {ex.Message}");
                    partial.AppendLine("Добавьте исполнителя вручную.");
                    return partial.ToString();
                }
            }

            // Start the task if autoStart is requested
            if (autoStart)
            {
                try
                {
                    await _client.PostActionAsync("ISimpleTasks", taskId, "Start", null);
                }
                catch (Exception ex)
                {
                    var partial = new StringBuilder();
                    partial.AppendLine(FormatCreatedTask(taskId, subject, importance, deadline, assigneeFullName));
                    partial.AppendLine();
                    partial.AppendLine($"**Внимание**: задача создана, но не удалось стартовать: {ex.Message}");
                    partial.AppendLine("Запустите задачу вручную.");
                    return partial.ToString();
                }
            }

            var result = new StringBuilder();
            result.AppendLine(FormatCreatedTask(taskId, subject, importance, deadline, assigneeFullName));
            if (autoStart)
                result.AppendLine("- **Статус**: Стартована");
            else
                result.AppendLine("- **Статус**: Черновик (не стартована)");

            return result.ToString();
        }
        catch (Exception ex)
        {
            return $"Ошибка при создании задачи: {ex.Message}";
        }
    }

    private async Task<(long? Id, string? Name)> LookupEmployeeAsync(string name)
    {
        var filter = $"contains(Name, '{EscapeOData(name)}')";
        var result = await _client.GetAsync(
            "IEmployees",
            filter: filter,
            select: "Id,Name",
            top: 5);

        var items = GetItems(result);
        if (items.Count == 0)
            return (null, null);

        // Prefer exact match if available, otherwise take first
        foreach (var item in items)
        {
            var empName = GetString(item, "Name");
            if (empName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return (GetLong(item, "Id"), empName);
        }

        // Return first match
        var first = items[0];
        return (GetLong(first, "Id"), GetString(first, "Name"));
    }

    private static string FormatCreatedTask(long taskId, string subject, string importance,
        string? deadline, string? assignee)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Задача создана.");
        sb.AppendLine();
        sb.AppendLine($"- **ID**: {taskId}");
        sb.AppendLine($"- **Тема**: {subject}");
        sb.AppendLine($"- **Важность**: {importance}");
        if (!string.IsNullOrWhiteSpace(deadline))
            sb.AppendLine($"- **Срок**: {deadline}");
        if (!string.IsNullOrWhiteSpace(assignee))
            sb.AppendLine($"- **Исполнитель**: {assignee}");
        return sb.ToString();
    }
}
