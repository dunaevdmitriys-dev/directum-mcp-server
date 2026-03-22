using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class AbsencesTool
{
    private readonly DirectumODataClient _client;
    public AbsencesTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "absences")]
    [Description("Кто отсутствует: отпуска, больничные, командировки. По дате, подразделению, типу.")]
    public async Task<string> Absences(
        [Description("Дата проверки (yyyy-MM-dd, по умолчанию сегодня)")] string? date = null,
        [Description("Фильтр по подразделению (часть названия)")] string? department = null,
        [Description("Тип: All, Vacation, SickLeave, BusinessTrip")] string type = "All",
        [Description("Макс. записей")] int top = 50)
    {
        var targetDate = string.IsNullOrWhiteSpace(date) ? DateTime.UtcNow : DateTime.Parse(date);
        var dateFilter = targetDate.ToString("yyyy-MM-dd");

        var sb = new StringBuilder();
        sb.AppendLine($"Отсутствующие на {targetDate:dd.MM.yyyy}");
        sb.AppendLine();

        try
        {
            // Try Absences entity (Sungero.Company.Absence)
            var filter = $"AbsenceSince le {dateFilter}T23:59:59Z and AbsenceTill ge {dateFilter}T00:00:00Z";

            var json = await _client.GetAsync("IAbsences",
                filter, "Id,AbsenceSince,AbsenceTill,AbsenceType",
                expand: "Employee($select=Id,Name;$expand=Department($select=Name))",
                top: top);

            if (!json.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
            {
                sb.AppendLine("Отсутствующих не найдено.");
                sb.AppendLine();
                sb.AppendLine("Возможные причины:");
                sb.AppendLine("- На эту дату нет записей об отсутствии");
                sb.AppendLine("- OData entity set `IAbsences` может иметь другое имя в вашей версии RX");
                return sb.ToString();
            }

            var absences = new List<(string Name, string Dept, string Type, string From, string To)>();

            foreach (var item in values.EnumerateArray())
            {
                var empName = "?";
                var deptName = "?";

                if (item.TryGetProperty("Employee", out var emp) && emp.ValueKind == JsonValueKind.Object)
                {
                    empName = emp.TryGetProperty("Name", out var en) ? en.GetString() ?? "?" : "?";
                    if (emp.TryGetProperty("Department", out var dept) && dept.ValueKind == JsonValueKind.Object)
                        deptName = dept.TryGetProperty("Name", out var dn) ? dn.GetString() ?? "?" : "?";
                }

                // Department filter
                if (!string.IsNullOrWhiteSpace(department) &&
                    !deptName.Contains(department, StringComparison.OrdinalIgnoreCase))
                    continue;

                var absType = item.TryGetProperty("AbsenceType", out var at) ? at.GetString() ?? "?" : "?";
                var from = item.TryGetProperty("AbsenceSince", out var af) ? af.GetString() ?? "" : "";
                var to = item.TryGetProperty("AbsenceTill", out var att) ? att.GetString() ?? "" : "";

                // Type filter
                if (type != "All" && !absType.Contains(type, StringComparison.OrdinalIgnoreCase))
                    continue;

                var typeRu = absType switch
                {
                    var t when t.Contains("Vacation", StringComparison.OrdinalIgnoreCase) => "Отпуск",
                    var t when t.Contains("Sick", StringComparison.OrdinalIgnoreCase) => "Больничный",
                    var t when t.Contains("Business", StringComparison.OrdinalIgnoreCase) => "Командировка",
                    var t when t.Contains("Trip", StringComparison.OrdinalIgnoreCase) => "Командировка",
                    _ => absType
                };

                var fromDate = DateTime.TryParse(from, out var fd) ? fd.ToString("dd.MM") : "?";
                var toDate = DateTime.TryParse(to, out var td) ? td.ToString("dd.MM") : "?";

                absences.Add((empName, deptName, typeRu, fromDate, toDate));
            }

            if (absences.Count == 0)
            {
                sb.AppendLine("По заданным фильтрам отсутствующих не найдено.");
                return sb.ToString();
            }

            // Group by department
            var grouped = absences.GroupBy(a => a.Dept).OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                sb.AppendLine($"{group.Key}:");
                foreach (var (name, _, absType, from, to) in group)
                    sb.AppendLine($"  {name} — {absType} ({from}–{to})");
                sb.AppendLine();
            }

            sb.AppendLine($"Всего: {absences.Count} чел.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Попробуйте `odata_query` с ручным запросом к IAbsences или используйте `search` с запросом \"отсутствующие\".");
        }

        return sb.ToString();
    }
}
