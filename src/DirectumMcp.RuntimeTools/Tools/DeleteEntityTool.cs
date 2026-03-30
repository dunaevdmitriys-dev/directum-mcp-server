using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.OData;
using ModelContextProtocol.Server;

namespace DirectumMcp.RuntimeTools.Tools;

[McpServerToolType]
public class DeleteEntityTool
{
    private readonly DirectumODataClient _client;
    public DeleteEntityTool(DirectumODataClient client) => _client = client;

    [McpServerTool(Name = "delete_entity")]
    [Description("Удалить сущность по ID. Только черновики и отменённые. Preview перед удалением.")]
    public async Task<string> DeleteEntity(
        [Description("OData тип сущности")] string entityType,
        [Description("ID сущности")] long entityId,
        [Description("Режим: preview (показать что удалится) или execute (удалить)")] string mode = "preview")
    {
        var sb = new StringBuilder();

        try
        {
            // 1. Get entity info
            var json = await _client.GetAsync(entityType, $"Id eq {entityId}",
                "Id,Name,Subject,Status,LifeCycleState");

            if (!json.TryGetProperty("value", out var values) || values.GetArrayLength() == 0)
                return $"Сущность {entityType}({entityId}) не найдена.";

            var item = values[0];
            var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" :
                       item.TryGetProperty("Subject", out var s) ? s.GetString() ?? "" : $"#{entityId}";
            var status = item.TryGetProperty("Status", out var st) ? st.GetString() ?? "" : "";
            var lifecycle = item.TryGetProperty("LifeCycleState", out var lc) ? lc.GetString() ?? "" : "";

            // 2. Safety check
            var isDraft = lifecycle is "Draft" or "" || status is "Draft" or "Aborted" or "Closed";
            if (!isDraft && mode == "execute")
            {
                sb.AppendLine($"ОТКАЗАНО: {entityType}({entityId}) — {name}");
                sb.AppendLine($"Статус: {status}, ЖЦ: {lifecycle}");
                sb.AppendLine();
                sb.AppendLine("Удаление разрешено только для черновиков (Draft), отменённых (Aborted) и закрытых (Closed).");
                sb.AppendLine("Для активных сущностей используйте update_entity для смены статуса.");
                return sb.ToString();
            }

            sb.AppendLine($"Удаление: {entityType}({entityId})");
            sb.AppendLine($"Название: {name}");
            sb.AppendLine($"Статус: {status}");
            if (!string.IsNullOrEmpty(lifecycle))
                sb.AppendLine($"Жизненный цикл: {lifecycle}");
            sb.AppendLine();

            if (mode == "preview")
            {
                sb.AppendLine(isDraft
                    ? "Можно удалить. Запустите с mode=execute."
                    : "ВНИМАНИЕ: сущность не в черновике. Удаление может быть заблокировано сервером.");
                return sb.ToString();
            }

            // 3. Execute DELETE
            await _client.DeleteAsync(entityType, entityId);
            sb.AppendLine("Удалено.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Ошибка: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Возможные причины:");
            sb.AppendLine("- Нет прав на удаление");
            sb.AppendLine("- Сущность используется в других записях");
            sb.AppendLine("- OData не поддерживает DELETE для этого типа");
        }

        return sb.ToString();
    }
}
