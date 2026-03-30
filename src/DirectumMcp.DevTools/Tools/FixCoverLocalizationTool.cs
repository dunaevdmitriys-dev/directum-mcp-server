using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;
using Npgsql;

namespace DirectumMcp.DevTools.Tools;

/// <summary>
/// Инструмент для диагностики и исправления заголовков/описаний обложек модулей RX.
///
/// Механизм разрешения заголовка обложки в RX 26.1:
/// 1. URL #/sat/cover/UUID → API module.cover?explorerModuleUuid=UUID
/// 2. moduleview WHERE explorermodule.uuid=UUID AND storeasdefault=true → mv.uuid + mv.cover(JSON)
/// 3. localization WHERE settingsid=mv.uuid → data(JSON)
/// 4. Title = data["_Title_{Header.NameGuid_без_дефисов}"]["ru-RU"]
/// 5. Description = data["_Description_{Header.NameGuid_без_дефисов}"]["ru-RU"]
///
/// ВАЖНО: поля Name и Description в локализации для заголовка обложки НЕ используются.
/// </summary>
[McpServerToolType]
public class FixCoverLocalizationTool
{
    [McpServerTool(Name = "fix_cover_localization")]
    [Description("Диагностирует и исправляет заголовки/описания обложек модулей Directum RX. " +
                 "Добавляет ключи _Title_ и _Description_ в sungero_settingslayer_localization. " +
                 "Требует прямого доступа к PostgreSQL.")]
    public async Task<string> FixCoverLocalization(
        [Description("Connection string к PostgreSQL. Пример: Host=85.198.82.172;Port=5432;Database=directum;Username=directum;Password=directum")]
        string connectionString,
        [Description("UUID модуля (explorerModuleUuid из URL #/sat/cover/<UUID>). Если пусто — диагностика всех модулей.")]
        string moduleUuid = "",
        [Description("Заголовок для обложки (ru-RU). Если пусто — только диагностика.")]
        string title = "",
        [Description("Описание для обложки (ru-RU). Если пусто — только диагностика.")]
        string description = "",
        [Description("Если true — применить изменения. Если false — только показать диагноз (по умолчанию false).")]
        bool apply = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Диагностика обложек модулей Directum RX");
        sb.AppendLine();

        await using var conn = new NpgsqlConnection(connectionString);
        try
        {
            await conn.OpenAsync();
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА подключения к БД**: {ex.Message}\n\nПроверьте connection string.";
        }

        if (string.IsNullOrWhiteSpace(moduleUuid))
            return await DiagnoseAll(conn, sb);

        return await FixModule(conn, sb, moduleUuid, title, description, apply);
    }

    private static async Task<string> DiagnoseAll(NpgsqlConnection conn, StringBuilder sb)
    {
        sb.AppendLine("### Все модули (default moduleviews)");
        sb.AppendLine();
        sb.AppendLine("| Модуль | mv.id | Header.NameGuid | _Title_ | _Description_ |");
        sb.AppendLine("|--------|-------|-----------------|---------|---------------|");

        const string sql = @"
SELECT em.name,
       mv.id,
       mv.cover,
       sl.data
FROM sungero_nocode_explorermodule em
JOIN sungero_nocode_moduleview mv ON mv.explorermodule = em.id AND mv.storeasdefault = true
LEFT JOIN sungero_settingslayer_localization sl ON sl.settingsid::text = mv.uuid::text
ORDER BY em.name";

        await using var cmd = new NpgsqlCommand(sql, conn);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var rowName = reader.GetString(0);
            var rowMvId = reader.GetInt64(1);
            var rowCover = reader.IsDBNull(2) ? null : reader.GetString(2);
            var rowData = reader.IsDBNull(3) ? null : reader.GetString(3);

            var rowGuid = GetHeaderNameGuid(rowCover);
            var rowKey = rowGuid?.Replace("-", "");
            bool rowHasTitle = false, rowHasDesc = false;

            if (rowData != null && rowKey != null)
            {
                try
                {
                    var node = JsonNode.Parse(rowData);
                    rowHasTitle = node?[$"_Title_{rowKey}"] != null;
                    rowHasDesc = node?[$"_Description_{rowKey}"] != null;
                }
                catch { }
            }

            sb.AppendLine($"| {rowName} | {rowMvId} | `{rowGuid ?? "?"}` | {(rowHasTitle ? "✅" : "❌")} | {(rowHasDesc ? "✅" : "❌")} |");
        }

        sb.AppendLine();
        sb.AppendLine("**Для исправления:**");
        sb.AppendLine("```");
        sb.AppendLine("fix_cover_localization connectionString=\"...\" moduleUuid=\"<UUID>\" title=\"Заголовок\" description=\"Описание\" apply=true");
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static async Task<string> FixModule(
        NpgsqlConnection conn, StringBuilder sb,
        string moduleUuid, string title, string description, bool apply)
    {
        sb.AppendLine($"**Модуль UUID:** `{moduleUuid}`");
        sb.AppendLine();

        // 1. Find explorermodule
        string? emName = null;
        long emId = -1;
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT id, name FROM sungero_nocode_explorermodule WHERE uuid::text = @uuid", conn);
            cmd.Parameters.AddWithValue("uuid", moduleUuid);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                emId = r.GetInt64(0);
                emName = r.GetString(1);
            }
        }

        if (emId < 0)
            return $"**ОШИБКА**: Модуль UUID `{moduleUuid}` не найден в sungero_nocode_explorermodule.";

        sb.AppendLine($"**Модуль:** {emName} (id={emId})");

        // 2. Find default moduleview
        long mvId = -1;
        string? mvUuid = null;
        string? coverJson = null;
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT id, uuid::text, cover FROM sungero_nocode_moduleview WHERE explorermodule = @eid AND storeasdefault = true LIMIT 1", conn);
            cmd.Parameters.AddWithValue("eid", emId);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                mvId = r.GetInt64(0);
                mvUuid = r.GetString(1);
                coverJson = r.IsDBNull(2) ? null : r.GetString(2);
            }
        }

        if (mvId < 0)
            return $"**ОШИБКА**: moduleview с storeasdefault=true для модуля id={emId} не найден.";

        sb.AppendLine($"**ModuleView:** id={mvId}, uuid=`{mvUuid}`");

        // 3. Get Header.NameGuid
        var headerGuid = GetHeaderNameGuid(coverJson);
        if (headerGuid == null)
            return $"**ОШИБКА**: Не удалось извлечь Header.NameGuid из cover JSON.";

        var headerKey = headerGuid.Replace("-", "");
        var titleKey = $"_Title_{headerKey}";
        var descKey = $"_Description_{headerKey}";

        sb.AppendLine($"**Header.NameGuid:** `{headerGuid}`");
        sb.AppendLine($"**_Title_ ключ:** `{titleKey}`");
        sb.AppendLine($"**_Description_ ключ:** `{descKey}`");
        sb.AppendLine();

        // 4. Check current localization
        string? currentData = null;
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT data FROM sungero_settingslayer_localization WHERE settingsid::text = @sid", conn);
            cmd.Parameters.AddWithValue("sid", mvUuid!);
            await using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
                currentData = r.GetString(0);
        }

        bool hasTitle = false, hasDesc = false;
        string? curTitle = null, curDesc = null;
        if (currentData != null)
        {
            try
            {
                var node = JsonNode.Parse(currentData);
                hasTitle = node?[titleKey] != null;
                hasDesc = node?[descKey] != null;
                curTitle = node?[titleKey]?["ru-RU"]?.GetValue<string>();
                curDesc = node?[descKey]?["ru-RU"]?.GetValue<string>();
            }
            catch { }
        }

        sb.AppendLine("### Текущее состояние");
        sb.AppendLine($"- `{titleKey}`: {(hasTitle ? $"✅ \"{curTitle}\"" : "❌ отсутствует")}");
        sb.AppendLine($"- `{descKey}`: {(hasDesc ? $"✅ \"{curDesc}\"" : "❌ отсутствует")}");
        sb.AppendLine();

        if (!apply)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                sb.AppendLine("Добавьте `title` и `apply=true` для исправления.");
                return sb.ToString();
            }

            sb.AppendLine("### SQL (dryRun — apply=false)");
            sb.AppendLine("```sql");
            sb.AppendLine(BuildSql(mvUuid!, titleKey, descKey, title, description ?? ""));
            sb.AppendLine("```");
            sb.AppendLine("Добавьте `apply=true` для применения.");
            return sb.ToString();
        }

        if (string.IsNullOrWhiteSpace(title))
            return "**ОШИБКА**: Параметр `title` обязателен при `apply=true`.";

        // 5. Apply changes
        try
        {
            if (currentData == null)
            {
                var newData = new JsonObject
                {
                    [titleKey] = new JsonObject { ["ru-RU"] = title },
                    [descKey] = new JsonObject { ["ru-RU"] = description ?? "" }
                };
                await using var insertCmd = new NpgsqlCommand(
                    "INSERT INTO sungero_settingslayer_localization (settingsid, data, lastupdate) VALUES (@sid, @data, NOW())", conn);
                insertCmd.Parameters.AddWithValue("sid", mvUuid!);
                insertCmd.Parameters.AddWithValue("data", newData.ToJsonString());
                await insertCmd.ExecuteNonQueryAsync();
                sb.AppendLine("### ✅ Применено (INSERT)");
            }
            else
            {
                await using var updCmd = new NpgsqlCommand(@"
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
    @tk::text, jsonb_build_object('ru-RU', @tv::text),
    @dk::text, jsonb_build_object('ru-RU', @dv::text)
))::citext,
lastupdate = NOW()
WHERE settingsid::text = @sid", conn);
                updCmd.Parameters.AddWithValue("tk", titleKey);
                updCmd.Parameters.AddWithValue("tv", title);
                updCmd.Parameters.AddWithValue("dk", descKey);
                updCmd.Parameters.AddWithValue("dv", description ?? "");
                updCmd.Parameters.AddWithValue("sid", mvUuid!);
                var rows = await updCmd.ExecuteNonQueryAsync();
                sb.AppendLine($"### ✅ Применено (UPDATE {rows} rows)");
            }

            sb.AppendLine($"- `{titleKey}` = \"{title}\"");
            sb.AppendLine($"- `{descKey}` = \"{description}\"");
            sb.AppendLine();
            sb.AppendLine("**Перезапустите webserver:**");
            sb.AppendLine("```bash");
            sb.AppendLine("docker restart sungerowebserver_directum");
            sb.AppendLine("```");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**ОШИБКА**: {ex.Message}");
        }

        return sb.ToString();
    }

    private static string? GetHeaderNameGuid(string? coverJson)
    {
        if (string.IsNullOrWhiteSpace(coverJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(coverJson);
            if (doc.RootElement.TryGetProperty("Header", out var header) &&
                header.TryGetProperty("NameGuid", out var ng))
                return ng.GetString();
        }
        catch { }
        return null;
    }

    private static string BuildSql(string settingsId, string titleKey, string descKey, string title, string description) =>
        $"""
UPDATE sungero_settingslayer_localization
SET data = (data::jsonb || jsonb_build_object(
  '{titleKey}', jsonb_build_object('ru-RU', '{title.Replace("'", "''")}'),
  '{descKey}',  jsonb_build_object('ru-RU', '{description.Replace("'", "''")}')
))::citext,
lastupdate = NOW()
WHERE settingsid = '{settingsId}';
""";
}
