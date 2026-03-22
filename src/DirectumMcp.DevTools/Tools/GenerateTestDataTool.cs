using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class GenerateTestDataTool
{
    [McpServerTool(Name = "generate_test_data")]
    [Description("Генерация SQL INSERT для тестовых данных: справочники, документы. Поддерживает связи и enum'ы. Для PostgreSQL.")]
    public async Task<string> GenerateTestData(
        [Description("Путь к .mtd файлу сущности (для автоматического определения полей)")] string entityPath = "",
        [Description("Имя таблицы БД (например 'DirRX_CRM_Deal'). Если пусто — определяется из .mtd")] string tableName = "",
        [Description("Поля и значения: 'Name:Сделка 1|Сделка 2|Сделка 3;Status:Active;Amount:100000|200000|500000'")] string data = "",
        [Description("Количество записей (по умолчанию определяется из data)")] int count = 0,
        [Description("Добавить Discriminator столбец (для DDS — всегда нужен)")] bool addDiscriminator = true)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Тестовые данные для Directum RX (PostgreSQL)");
        sb.AppendLine("-- Сгенерировано MCP generate_test_data");
        sb.AppendLine();

        // Parse entity schema from .mtd if provided
        var columns = new List<ColumnDef>();
        string resolvedTableName = tableName;

        if (!string.IsNullOrWhiteSpace(entityPath) && File.Exists(entityPath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(entityPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                var moduleName = root.TryGetProperty("ModuleName", out var m) ? m.GetString() ?? "" : "";

                if (string.IsNullOrWhiteSpace(resolvedTableName))
                    resolvedTableName = $"{moduleName.Replace(".", "_")}_{entityName}";

                // Extract properties
                if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prop in props.EnumerateArray())
                    {
                        var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "" : "";
                        var propType = prop.TryGetProperty("$type", out var pt) ? pt.GetString() ?? "" : "";
                        var isRequired = prop.TryGetProperty("IsRequired", out var req) && req.GetBoolean();

                        var sqlType = MapToSqlType(propType);
                        columns.Add(new ColumnDef(propName, sqlType, isRequired));
                    }
                }
            }
            catch { }
        }

        if (string.IsNullOrWhiteSpace(resolvedTableName))
            resolvedTableName = "MyEntity";

        // Parse data values
        var dataColumns = ParseDataValues(data);

        // Determine count
        if (count <= 0)
        {
            count = dataColumns.Count > 0
                ? dataColumns.Max(c => c.Values.Count)
                : 5;
        }

        // Generate SQL
        sb.AppendLine($"-- Таблица: {resolvedTableName}");
        sb.AppendLine($"-- Записей: {count}");
        sb.AppendLine();

        // Build column list
        var allColumns = new List<string> { "Id" };
        if (addDiscriminator)
            allColumns.Add("Discriminator");

        // Add Status for DatabookEntry
        if (!dataColumns.Any(c => c.Name.Equals("Status", StringComparison.OrdinalIgnoreCase)))
            allColumns.Add("Status");

        foreach (var dc in dataColumns)
            allColumns.Add(dc.Name);

        // Add known columns from .mtd that aren't in data
        foreach (var col in columns.Where(c => !dataColumns.Any(d => d.Name.Equals(c.Name, StringComparison.OrdinalIgnoreCase))
                                              && !allColumns.Contains(c.Name, StringComparer.OrdinalIgnoreCase)))
        {
            if (col.Name is "Name" or "Subject" or "Description")
                allColumns.Add(col.Name);
        }

        sb.AppendLine("BEGIN;");
        sb.AppendLine();

        for (int i = 0; i < count; i++)
        {
            var values = new List<string>();

            foreach (var col in allColumns)
            {
                if (col == "Id")
                {
                    values.Add($"nextval('{resolvedTableName}_id_seq')");
                    continue;
                }
                if (col == "Discriminator")
                {
                    values.Add($"'{resolvedTableName}'");
                    continue;
                }
                if (col == "Status")
                {
                    values.Add("'Active'");
                    continue;
                }

                var dataCol = dataColumns.FirstOrDefault(d => d.Name.Equals(col, StringComparison.OrdinalIgnoreCase));
                if (dataCol != null && dataCol.Values.Count > 0)
                {
                    var val = dataCol.Values[i % dataCol.Values.Count];
                    if (double.TryParse(val, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out _))
                        values.Add(val);
                    else if (val.Equals("null", StringComparison.OrdinalIgnoreCase))
                        values.Add("NULL");
                    else if (val.Equals("true", StringComparison.OrdinalIgnoreCase))
                        values.Add("TRUE");
                    else if (val.Equals("false", StringComparison.OrdinalIgnoreCase))
                        values.Add("FALSE");
                    else
                        values.Add($"'{val.Replace("'", "''")}'");
                }
                else
                {
                    // Auto-generate
                    var colDef = columns.FirstOrDefault(c => c.Name.Equals(col, StringComparison.OrdinalIgnoreCase));
                    values.Add(colDef?.SqlType switch
                    {
                        "integer" or "bigint" => (i + 1).ToString(),
                        "numeric" => ((i + 1) * 1000).ToString(),
                        "boolean" => "TRUE",
                        "timestamp" => $"NOW() - INTERVAL '{count - i} days'",
                        _ => $"'{col} {i + 1}'"
                    });
                }
            }

            sb.AppendLine($"INSERT INTO \"{resolvedTableName}\" ({string.Join(", ", allColumns.Select(c => $"\"{c}\""))})");
            sb.AppendLine($"VALUES ({string.Join(", ", values)});");
        }

        sb.AppendLine();
        sb.AppendLine("COMMIT;");
        sb.AppendLine();
        sb.AppendLine($"-- Проверка: SELECT * FROM \"{resolvedTableName}\" ORDER BY \"Id\" DESC LIMIT {count};");

        // Report
        var report = new StringBuilder();
        report.AppendLine("## Тестовые данные сгенерированы");
        report.AppendLine();
        report.AppendLine($"**Таблица:** `{resolvedTableName}`");
        report.AppendLine($"**Записей:** {count}");
        report.AppendLine($"**Столбцов:** {allColumns.Count}");
        report.AppendLine();
        report.AppendLine("### SQL");
        report.AppendLine("```sql");
        report.Append(sb);
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine("### Использование");
        report.AppendLine("```bash");
        report.AppendLine($"psql -U postgres -d directum_rx -f test_data.sql");
        report.AppendLine("```");
        report.AppendLine();
        report.AppendLine("**ВНИМАНИЕ:** Перед выполнением остановите сервисы Directum RX.");
        report.AppendLine("После вставки перезапустите сервисы для обновления кэша.");

        return report.ToString();
    }

    private static string MapToSqlType(string metadataType)
    {
        if (metadataType.Contains("String")) return "text";
        if (metadataType.Contains("Text")) return "text";
        if (metadataType.Contains("Integer")) return "integer";
        if (metadataType.Contains("LongInteger")) return "bigint";
        if (metadataType.Contains("Double")) return "numeric";
        if (metadataType.Contains("Boolean")) return "boolean";
        if (metadataType.Contains("DateTime")) return "timestamp";
        if (metadataType.Contains("Navigation")) return "bigint"; // FK
        if (metadataType.Contains("Enum")) return "text";
        return "text";
    }

    private static List<DataColumn> ParseDataValues(string data)
    {
        var result = new List<DataColumn>();
        if (string.IsNullOrWhiteSpace(data)) return result;

        foreach (var part in data.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx <= 0) continue;

            var name = part[..colonIdx].Trim();
            var values = part[(colonIdx + 1)..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            result.Add(new DataColumn(name, values));
        }
        return result;
    }

    private record ColumnDef(string Name, string SqlType, bool IsRequired);
    private record DataColumn(string Name, List<string> Values);
}
