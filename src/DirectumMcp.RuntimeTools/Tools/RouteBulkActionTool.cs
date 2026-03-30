using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class RouteBulkActionTool
{
    private readonly DirectumODataClient _client;
    public RouteBulkActionTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "route_bulk_action")]
    [Description("Массовая маршрутизация заданий: переадресация/выполнение по фильтру. Preview перед выполнением.")]
    public async Task<string> RouteBulkAction(
        [Description("OData $filter для выбора заданий")] string filter,
        [Description("Действие: forward (переадресовать) или complete (выполнить)")] string action = "forward",
        [Description("ID сотрудника для переадресации (для action=forward)")] long forwardToId = 0,
        [Description("Режим: preview (показать что будет) или execute (выполнить)")] string mode = "preview",
        [Description("Максимум заданий для обработки")] int limit = 50)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Массовая маршрутизация заданий");
        sb.AppendLine();

        try
        {
            var json = await _client.GetAsync("IAssignments",
                $"$filter={filter} and Status eq 'InProcess'&$select=Id,Subject,Deadline&$expand=Performer($select=Id,Name),Author($select=Id,Name)&$top={limit}&$orderby=Created desc");

            if (json.ValueKind == JsonValueKind.Undefined)
            {
                sb.AppendLine("Не удалось получить данные.");
                return sb.ToString();
            }

            var values = json.GetProperty("value");
            var count = values.GetArrayLength();

            sb.AppendLine($"**Фильтр:** `{filter}`");
            sb.AppendLine($"**Действие:** {action}");
            sb.AppendLine($"**Найдено заданий:** {count}");
            sb.AppendLine($"**Режим:** {mode}");
            sb.AppendLine();

            if (count == 0)
            {
                sb.AppendLine("Заданий, соответствующих фильтру, не найдено.");
                return sb.ToString();
            }

            // Preview
            sb.AppendLine("| # | ID | Тема | Исполнитель | Автор |");
            sb.AppendLine("|---|-----|------|-------------|-------|");

            var assignments = new List<(long Id, string Subject)>();
            int idx = 0;
            foreach (var item in values.EnumerateArray())
            {
                idx++;
                var id = item.TryGetProperty("Id", out var idEl) ? idEl.GetInt64() : 0;
                var subj = item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "?" : "?";
                var perf = "?";
                if (item.TryGetProperty("Performer", out var p) && p.ValueKind == JsonValueKind.Object)
                    perf = p.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";
                var auth = "?";
                if (item.TryGetProperty("Author", out var a) && a.ValueKind == JsonValueKind.Object)
                    auth = a.TryGetProperty("Name", out var an) ? an.GetString() ?? "?" : "?";

                assignments.Add((id, subj));
                sb.AppendLine($"| {idx} | #{id} | {Truncate(subj, 35)} | {perf} | {auth} |");
            }
            sb.AppendLine();

            if (mode == "preview")
            {
                sb.AppendLine($"Для выполнения запустите с `mode=execute`.");
                if (action == "forward" && forwardToId == 0)
                    sb.AppendLine("**ВНИМАНИЕ:** Укажите `forwardToId` для переадресации.");
                return sb.ToString();
            }

            // Execute
            sb.AppendLine("## Результат");
            int success = 0, errors = 0;

            foreach (var (id, subj) in assignments)
            {
                try
                {
                    if (action == "forward")
                    {
                        if (forwardToId == 0)
                        {
                            sb.AppendLine($"- #{id} → **ПРОПУЩЕНО**: не указан forwardToId");
                            errors++;
                            continue;
                        }
                        await _client.PostActionAsync("IAssignments", id, "Forward",
                            JsonSerializer.Serialize(new { ForwardTo = new { Id = forwardToId } }));
                        sb.AppendLine($"- #{id} → переадресовано");
                    }
                    else // complete
                    {
                        await _client.PostActionAsync("IAssignments", id, "Complete",
                            JsonSerializer.Serialize(new { Result = "Completed" }));
                        sb.AppendLine($"- #{id} → выполнено");
                    }
                    success++;
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"- #{id} → **ОШИБКА**: {ex.Message}");
                    errors++;
                }
            }

            sb.AppendLine();
            sb.AppendLine($"**Успешно:** {success} | **Ошибок:** {errors}");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**Ошибка:** {ex.Message}");
        }

        return sb.ToString();
    }

    private static string Truncate(string s, int max) =>
        s.Length > max ? s[..max] + "..." : s;
}
