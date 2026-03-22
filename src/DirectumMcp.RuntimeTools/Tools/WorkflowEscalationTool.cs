using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class WorkflowEscalationTool
{
    private readonly DirectumODataClient _client;
    public WorkflowEscalationTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "workflow_escalation")]
    [Description("Эскалация просроченных заданий: найти просроченные, определить руководителя, предложить переадресацию.")]
    public async Task<string> EscalateOverdue(
        [Description("Минимальное количество дней просрочки для эскалации")] int overdueDays = 2,
        [Description("ID подразделения (опционально)")] long departmentId = 0,
        [Description("Режим: report (только отчёт) или execute (переадресовать)")] string mode = "report")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Эскалация просроченных заданий");
        sb.AppendLine();

        try
        {
            var now = DateTime.UtcNow;
            var threshold = now.AddDays(-overdueDays).ToString("yyyy-MM-ddTHH:mm:ssZ");

            var filter = $"Status eq 'InProcess' and Deadline lt {threshold}";
            if (departmentId > 0)
                filter += $" and Performer/Department/Id eq {departmentId}";

            var json = await _client.GetAsync("IAssignments",
                $"$filter={filter}&$expand=Performer($select=Id,Name;$expand=Department($select=Id,Name,Manager($select=Id,Name))),Task($select=Id,Subject),Author($select=Id,Name)&$top=100&$orderby=Deadline asc");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine("Не удалось получить данные. Проверьте OData.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            var escalations = new List<EscalationItem>();

            foreach (var item in values.EnumerateArray())
            {
                var assignId = item.TryGetProperty("Id", out var id) ? id.GetInt64() : 0;
                var subject = "?";
                if (item.TryGetProperty("Task", out var task) && task.ValueKind == JsonValueKind.Object)
                    subject = task.TryGetProperty("Subject", out var s) ? s.GetString() ?? "?" : "?";

                var performerName = "?";
                var managerId = 0L;
                var managerName = "?";
                var deptName = "?";

                if (item.TryGetProperty("Performer", out var perf) && perf.ValueKind == JsonValueKind.Object)
                {
                    performerName = perf.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                    if (perf.TryGetProperty("Department", out var dept) && dept.ValueKind == JsonValueKind.Object)
                    {
                        deptName = dept.TryGetProperty("Name", out var dn) ? dn.GetString() ?? "?" : "?";
                        if (dept.TryGetProperty("Manager", out var mgr) && mgr.ValueKind == JsonValueKind.Object)
                        {
                            managerId = mgr.TryGetProperty("Id", out var mid) ? mid.GetInt64() : 0;
                            managerName = mgr.TryGetProperty("Name", out var mn) ? mn.GetString() ?? "?" : "?";
                        }
                    }
                }

                var deadline = item.TryGetProperty("Deadline", out var dl) && dl.ValueKind == JsonValueKind.String
                    ? dl.GetString() ?? "" : "";

                var daysOverdue = 0;
                if (DateTime.TryParse(deadline, out var deadlineDate))
                    daysOverdue = (int)(now - deadlineDate).TotalDays;

                escalations.Add(new EscalationItem(
                    assignId, subject, performerName, deptName,
                    managerId, managerName, daysOverdue, deadline));
            }

            sb.AppendLine($"**Просрочка >{overdueDays} дней:** {escalations.Count} заданий");
            sb.AppendLine($"**Режим:** {mode}");
            sb.AppendLine();

            if (escalations.Count == 0)
            {
                sb.AppendLine("Просроченных заданий не найдено. Всё в порядке.");
                return sb.ToString();
            }

            sb.AppendLine("| # | Задание | Тема | Исполнитель | Подразделение | Просрочка | Руководитель |");
            sb.AppendLine("|---|---------|------|-------------|---------------|-----------|-------------|");

            foreach (var (i, e) in escalations.Select((e, i) => (i + 1, e)))
            {
                sb.AppendLine($"| {i} | #{e.AssignmentId} | {Truncate(e.Subject, 30)} | {e.PerformerName} | {e.DeptName} | {e.DaysOverdue}д | {e.ManagerName} |");
            }
            sb.AppendLine();

            if (mode == "execute")
            {
                sb.AppendLine("## Результат эскалации");
                var forwarded = 0;
                foreach (var e in escalations.Where(e => e.ManagerId > 0))
                {
                    try
                    {
                        await _client.PostActionAsync("IAssignments", e.AssignmentId, "Forward",
                            JsonSerializer.Serialize(new { ForwardTo = new { Id = e.ManagerId } }));
                        sb.AppendLine($"- #{e.AssignmentId} → переадресовано {e.ManagerName}");
                        forwarded++;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"- #{e.AssignmentId} → **ОШИБКА**: {ex.Message}");
                    }
                }
                sb.AppendLine();
                sb.AppendLine($"**Переадресовано:** {forwarded}/{escalations.Count}");
            }
            else
            {
                sb.AppendLine("## Рекомендации");
                sb.AppendLine("Для автоматической переадресации запустите с `mode=execute`.");
                sb.AppendLine("Задания будут переадресованы руководителю подразделения исполнителя.");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "..." : s;

    private record EscalationItem(long AssignmentId, string Subject, string PerformerName,
        string DeptName, long ManagerId, string ManagerName, int DaysOverdue, string Deadline);
}
