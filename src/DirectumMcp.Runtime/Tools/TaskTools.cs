using System.ComponentModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

using static DirectumMcp.Core.Helpers.ODataHelpers;

namespace DirectumMcp.Runtime.Tools;

[McpServerToolType]
public class TaskTools
{
    private readonly DirectumODataClient _client;

    public TaskTools(DirectumODataClient client)
    {
        _client = client;
    }

    #region MyAssignmentsTool

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

            return FormatMyAssignmentsResults(result, status);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось получить задания. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private static string FormatMyAssignmentsResults(JsonElement result, string status)
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
                if (isOverdue) overdueCount++;
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

    #endregion

    #region CreateTaskTool

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
            if (!AllowedImportance.Contains(importance))
                return $"Ошибка: недопустимая важность '{importance}'. Допустимые значения: {string.Join(", ", AllowedImportance)}.";

            if (!string.IsNullOrWhiteSpace(deadline) &&
                !DateTime.TryParseExact(deadline, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) &&
                !DateTime.TryParseExact(deadline, "yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                return "Ошибка: параметр deadline должен быть в формате yyyy-MM-dd или yyyy-MM-ddTHH:mm:ss.";

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

            var taskBody = new Dictionary<string, object?>
            {
                ["Subject"] = subject,
                ["Importance"] = importance
            };

            if (!string.IsNullOrWhiteSpace(description))
                taskBody["ActiveText"] = description;

            if (!string.IsNullOrWhiteSpace(deadline))
            {
                if (!deadline.Contains('T'))
                    deadline += "T23:59:59Z";
                else if (!deadline.EndsWith('Z') && !deadline.Contains('+'))
                    deadline += "Z";
                taskBody["MaxDeadline"] = deadline;
            }

            var createdTask = await _client.PostAsync("ISimpleTasks", taskBody);
            var taskId = GetLong(createdTask, "Id");

            if (taskId == 0)
                return "Ошибка: задача создана, но не удалось получить ID.";

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
                    var partial = new StringBuilder();
                    partial.AppendLine($"Задача создана (ID: {taskId}), но не удалось назначить исполнителя: {ex.Message}");
                    partial.AppendLine("Добавьте исполнителя вручную.");
                    return partial.ToString();
                }
            }

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
            return $"**ОШИБКА**: Не удалось создать задачу. Проверьте переменные окружения RX_ODATA_URL, RX_USERNAME, RX_PASSWORD. Детали: {ex.Message}";
        }
    }

    private async Task<(long? Id, string? Name)> LookupEmployeeAsync(string name)
    {
        var filter = $"contains(Name, '{EscapeOData(name)}')";
        var result = await _client.GetAsync("IEmployees", filter: filter, select: "Id,Name", top: 5);
        var items = GetItems(result);
        if (items.Count == 0) return (null, null);

        foreach (var item in items)
        {
            var empName = GetString(item, "Name");
            if (empName.Equals(name, StringComparison.OrdinalIgnoreCase))
                return (GetLong(item, "Id"), empName);
        }
        var first = items[0];
        return (GetLong(first, "Id"), GetString(first, "Name"));
    }

    private static string FormatCreatedTask(long taskId, string subject, string importance, string? deadline, string? assignee)
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

    #endregion

    #region CompleteAssignmentTool

    [McpServerTool(Name = "complete")]
    [Description("Выполнить задание в Directum RX")]
    public async Task<string> Complete(
        [Description("ID задания")] long assignmentId,
        [Description("Результат выполнения: Complete, ForRevision, Abort, Explored и др.")] string result = "Complete",
        [Description("Текст комментария при выполнении")] string? activeText = null)
    {
        try
        {
            var assignment = await _client.GetByIdAsync("IAssignments", assignmentId);
            var currentStatus = GetString(assignment, "Status");
            if (currentStatus != "InProcess")
                return $"Задание {assignmentId} нельзя выполнить: текущий статус '{currentStatus}' (ожидается 'InProcess').";

            var subject = GetString(assignment, "Subject");
            var actionBody = new Dictionary<string, object?> { ["Result"] = result };
            if (!string.IsNullOrWhiteSpace(activeText))
                actionBody["ActiveText"] = activeText;

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

    #endregion

    #region DelegateTool

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

            var body = new { ForwardTo = new { Id = delegateToId } };
            var resultJson = await _client.PostActionAsync("IAssignments", assignmentId, "Forward",
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

    #endregion

    #region CreateActionItemTool

    [McpServerTool(Name = "create_action_item")]
    [Description("Назначить поручение сотруднику: тема, срок, важность. Создаёт ActionItemExecutionTask и автоматически стартует.")]
    public async Task<string> CreateActionItem(
        [Description("Тема поручения")] string subject,
        [Description("Имя или часть имени исполнителя (например 'Иванов')")] string assigneeName,
        [Description("Срок (yyyy-MM-dd или 'пятница', 'через 3 дня')")] string deadline,
        [Description("Текст поручения (подробное описание)")] string description = "",
        [Description("Важность: High, Normal, Low")] string importance = "Normal")
    {
        var sb = new StringBuilder();
        try
        {
            var empJson = await _client.GetAsync("IEmployees",
                $"contains(Name, '{assigneeName}')", "Id,Name,Status",
                expand: "Department($select=Name)");

            if (!empJson.TryGetProperty("value", out var empValues) || empValues.GetArrayLength() == 0)
                return $"Сотрудник `{assigneeName}` не найден. Проверьте имя.";

            var employees = new List<(long Id, string Name, string Dept)>();
            foreach (var emp in empValues.EnumerateArray())
            {
                var empId = emp.TryGetProperty("Id", out var eid) ? eid.GetInt64() : 0;
                var empName = emp.TryGetProperty("Name", out var en) ? en.GetString() ?? "" : "";
                var empStatus = emp.TryGetProperty("Status", out var es) ? es.GetString() ?? "" : "";
                var deptName = "";
                if (emp.TryGetProperty("Department", out var d) && d.ValueKind == JsonValueKind.Object)
                    deptName = d.TryGetProperty("Name", out var dn) ? dn.GetString() ?? "" : "";
                if (empStatus != "Closed")
                    employees.Add((empId, empName, deptName));
            }

            if (employees.Count == 0)
                return $"Сотрудник `{assigneeName}` закрыт или не найден.";

            if (employees.Count > 1)
            {
                sb.AppendLine($"Найдено {employees.Count} сотрудников по запросу `{assigneeName}`:");
                foreach (var (id, name, dept) in employees)
                    sb.AppendLine($"- {name} (#{id}, {dept})");
                sb.AppendLine();
                sb.AppendLine("Уточните имя или укажите полное ФИО.");
                return sb.ToString();
            }

            var assignee = employees[0];
            var deadlineDate = ParseDeadline(deadline);
            if (deadlineDate == null)
                return $"Не удалось распознать срок `{deadline}`. Формат: yyyy-MM-dd или 'пятница', 'через 3 дня'.";

            var taskBody = new Dictionary<string, object>
            {
                ["Subject"] = subject,
                ["Importance"] = importance,
                ["MaxDeadline"] = deadlineDate.Value.ToString("yyyy-MM-ddT18:00:00Z"),
                ["Assignee"] = new { Id = assignee.Id }
            };
            if (!string.IsNullOrWhiteSpace(description))
                taskBody["ActiveText"] = description;

            var resultJson = await _client.PostAsync("IActionItemExecutionTasks", taskBody);
            var taskId = resultJson.TryGetProperty("Id", out var tid) ? tid.GetInt64() : 0;

            try { await _client.PostActionAsync("IActionItemExecutionTasks", taskId, "Start", null); }
            catch { }

            sb.AppendLine("Поручение создано");
            sb.AppendLine();
            sb.AppendLine($"ID: #{taskId}");
            sb.AppendLine($"Тема: {subject}");
            sb.AppendLine($"Исполнитель: {assignee.Name} ({assignee.Dept})");
            sb.AppendLine($"Срок: {deadlineDate.Value:dd.MM.yyyy}");
            sb.AppendLine($"Важность: {importance}");
            if (!string.IsNullOrWhiteSpace(description))
                sb.AppendLine($"Описание: {description}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
        }
        return sb.ToString();
    }

    private static DateTime? ParseDeadline(string input)
    {
        if (DateTime.TryParse(input, out var parsed)) return parsed;
        var lower = input.ToLowerInvariant().Trim();
        var today = DateTime.UtcNow.Date;

        if (lower is "сегодня" or "today") return today;
        if (lower is "завтра" or "tomorrow") return today.AddDays(1);
        if (lower is "послезавтра") return today.AddDays(2);

        if (lower.StartsWith("через ") && lower.EndsWith(" дней") || lower.EndsWith(" дня") || lower.EndsWith(" день"))
        {
            var parts = lower.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var days))
                return today.AddDays(days);
        }

        var weekdays = new Dictionary<string, DayOfWeek>
        {
            ["понедельник"] = DayOfWeek.Monday, ["вторник"] = DayOfWeek.Tuesday,
            ["среда"] = DayOfWeek.Wednesday, ["среду"] = DayOfWeek.Wednesday,
            ["четверг"] = DayOfWeek.Thursday, ["пятница"] = DayOfWeek.Friday, ["пятницу"] = DayOfWeek.Friday,
            ["суббота"] = DayOfWeek.Saturday, ["субботу"] = DayOfWeek.Saturday,
            ["воскресенье"] = DayOfWeek.Sunday,
        };

        foreach (var (name, dow) in weekdays)
        {
            if (lower.Contains(name))
            {
                var daysUntil = ((int)dow - (int)today.DayOfWeek + 7) % 7;
                if (daysUntil == 0) daysUntil = 7;
                return today.AddDays(daysUntil);
            }
        }
        return null;
    }

    #endregion
}
