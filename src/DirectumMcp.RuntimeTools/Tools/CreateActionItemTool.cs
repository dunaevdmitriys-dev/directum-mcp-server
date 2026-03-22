using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class CreateActionItemTool
{
    private readonly DirectumODataClient _client;
    public CreateActionItemTool(DirectumODataClient client) => _client = client;

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
            // 1. Find employee
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

            // 2. Parse deadline
            var deadlineDate = ParseDeadline(deadline);
            if (deadlineDate == null)
                return $"Не удалось распознать срок `{deadline}`. Формат: yyyy-MM-dd или 'пятница', 'через 3 дня'.";

            // 3. Create task
            var taskBody = new Dictionary<string, object>
            {
                ["Subject"] = subject,
                ["Importance"] = importance,
                ["MaxDeadline"] = deadlineDate.Value.ToString("yyyy-MM-ddT18:00:00Z"),
                ["Assignee"] = new { Id = assignee.Id }
            };

            if (!string.IsNullOrWhiteSpace(description))
                taskBody["ActiveText"] = description;

            var result = await _client.PostAsync("IActionItemExecutionTasks", taskBody);

            var taskId = result.TryGetProperty("Id", out var tid) ? tid.GetInt64() : 0;

            // 4. Auto-start
            try
            {
                await _client.PostActionAsync("IActionItemExecutionTasks", taskId, "Start", null);
            }
            catch
            {
                // May fail if auto-start not supported — ok, task created
            }

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
        if (DateTime.TryParse(input, out var parsed))
            return parsed;

        var lower = input.ToLowerInvariant().Trim();
        var today = DateTime.UtcNow.Date;

        if (lower is "сегодня" or "today") return today;
        if (lower is "завтра" or "tomorrow") return today.AddDays(1);
        if (lower is "послезавтра") return today.AddDays(2);

        // "через N дней"
        if (lower.StartsWith("через ") && lower.EndsWith(" дней") || lower.EndsWith(" дня") || lower.EndsWith(" день"))
        {
            var parts = lower.Split(' ');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var days))
                return today.AddDays(days);
        }

        // Weekday names
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
}
