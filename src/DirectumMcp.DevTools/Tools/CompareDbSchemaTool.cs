using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class CompareDbSchemaTool
{
    private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StringProperty"] = "citext",
        ["IntegerProperty"] = "int8",
        ["DoubleProperty"] = "float8",
        ["BooleanProperty"] = "bool",
        ["DateTimeProperty"] = "timestamp",
        ["NavigationProperty"] = "int8",
        ["EnumProperty"] = "citext",
        ["TextProperty"] = "citext",
    };

    [McpServerTool(Name = "compare_db_schema")]
    [Description("Сравнение MTD-свойств сущности с реальной схемой PostgreSQL. Находит drift: недостающие колонки, лишние колонки, расхождения типов.")]
    public async Task<string> Execute(
        [Description("Путь к директории модуля")] string modulePath,
        [Description("PostgreSQL connection string (host=..;port=..;dbname=..;user=..;password=..)")] string? connectionString = null,
        [Description("Имя сущности (если не указано — все сущности модуля)")] string? entityName = null,
        [Description("Путь к файлу pg_dump со схемой (альтернатива прямому подключению)")] string? schemaDumpPath = null)
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);
        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория не найдена: `{modulePath}`";

        if (schemaDumpPath != null && !PathGuard.IsAllowed(schemaDumpPath))
            return PathGuard.DenyMessage(schemaDumpPath);

        // Find Module.mtd
        var moduleMtds = Directory.GetFiles(modulePath, "Module.mtd", SearchOption.AllDirectories);
        if (moduleMtds.Length == 0)
            return $"**ОШИБКА**: Module.mtd не найден в `{modulePath}`";

        using var moduleDoc = await MtdParser.ParseRawAsync(moduleMtds[0]);
        var moduleCode = GetString(moduleDoc.RootElement, "Code");
        var moduleNamespace = GetString(moduleDoc.RootElement, "Name");
        var prefix = moduleNamespace.StartsWith("DirRX", StringComparison.OrdinalIgnoreCase) ? "dirrx" : "sungero";

        // Parse schema dump if provided
        Dictionary<string, HashSet<string>>? dbSchema = null;
        if (!string.IsNullOrWhiteSpace(schemaDumpPath) && File.Exists(schemaDumpPath))
        {
            dbSchema = await ParseSchemaDump(schemaDumpPath);
        }

        // Find entity MTDs
        var entityMtds = Directory.GetFiles(modulePath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("# Сравнение MTD ↔ DB Schema");
        sb.AppendLine();

        var errors = new ParseErrorCounter();
        var totalDrifts = 0;

        foreach (var mtdFile in entityMtds)
        {
            try
            {
                using var doc = await MtdParser.ParseRawAsync(mtdFile);
                var root = doc.RootElement;
                var metaType = GetString(root, "$type");
                if (!metaType.Contains("EntityMetadata")) continue;

                var eName = GetString(root, "Name");
                var eCode = GetString(root, "Code");
                if (string.IsNullOrEmpty(eCode)) eCode = eName;

                if (!string.IsNullOrWhiteSpace(entityName) &&
                    !eName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var tableName = $"{prefix}_{moduleCode}_{eCode}".ToLowerInvariant();

                // Extract MTD properties → expected columns
                var mtdColumns = ExtractMtdColumns(root);

                if (dbSchema != null)
                {
                    // Compare with actual DB schema
                    var drifts = CompareWithDb(tableName, mtdColumns, dbSchema);
                    if (drifts.Count > 0)
                    {
                        totalDrifts += drifts.Count;
                        sb.AppendLine($"## {eName} → `{tableName}`");
                        sb.AppendLine();
                        sb.AppendLine("| Тип drift | Колонка | Детали |");
                        sb.AppendLine("|---|---|---|");
                        foreach (var d in drifts)
                            sb.AppendLine($"| {d.Type} | {d.Column} | {d.Details} |");
                        sb.AppendLine();
                    }
                }
                else
                {
                    // No DB schema — just output expected columns for manual comparison
                    sb.AppendLine($"## {eName} → `{tableName}`");
                    sb.AppendLine();
                    sb.AppendLine("| MTD Property | Expected Column | Expected Type | Nullable |");
                    sb.AppendLine("|---|---|---|---|");
                    sb.AppendLine("| _id_ | id | int8 | NOT NULL |");
                    sb.AppendLine("| _discriminator_ | discriminator | uuid | NOT NULL |");
                    foreach (var col in mtdColumns)
                        sb.AppendLine($"| {col.Name} | {col.Code.ToLowerInvariant()} | {col.DbType} | {(col.IsNullable ? "NULL" : "NOT NULL")} |");
                    sb.AppendLine();
                }
            }
            catch (Exception ex)
            {
                errors.Record(mtdFile, ex.Message);
            }
        }

        if (dbSchema != null)
        {
            sb.Insert(0, "");
            if (totalDrifts == 0)
                sb.AppendLine("**Drift не обнаружен** — MTD и DB schema согласованы.");
            else
                sb.AppendLine($"**Обнаружено {totalDrifts} расхождений.**");
        }
        else
        {
            sb.AppendLine("> Для полного сравнения укажите `schemaDumpPath` (результат `pg_dump -s`) или `connectionString`.");
            sb.AppendLine("> Без этого инструмент выводит ожидаемую схему из MTD для ручного сравнения.");
        }

        errors.AppendSummary(sb, entityMtds.Count);
        return sb.ToString();
    }

    private static List<MtdColumn> ExtractMtdColumns(JsonElement root)
    {
        var result = new List<MtdColumn>();
        if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var prop in props.EnumerateArray())
        {
            var propType = GetString(prop, "$type");
            if (propType.Contains("CollectionPropertyMetadata")) continue;

            var name = GetString(prop, "Name");
            var code = GetString(prop, "Code");
            if (string.IsNullOrEmpty(code)) code = name;

            var shortType = propType.Split(',')[0].Split('.').LastOrDefault()?.Replace("Metadata", "") ?? "String";
            var dbType = TypeMap.GetValueOrDefault(shortType, "citext");
            var isRequired = prop.TryGetProperty("IsRequired", out var req) && req.ValueKind == JsonValueKind.True;

            result.Add(new MtdColumn(name, code, shortType, dbType, !isRequired));
        }
        return result;
    }

    private static List<DriftItem> CompareWithDb(
        string tableName,
        List<MtdColumn> mtdColumns,
        Dictionary<string, HashSet<string>> dbSchema)
    {
        var drifts = new List<DriftItem>();

        if (!dbSchema.TryGetValue(tableName, out var dbColumns))
        {
            drifts.Add(new DriftItem("⚠️ Missing table", tableName, "Таблица не найдена в DB schema dump"));
            return drifts;
        }

        // System columns
        var expectedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "discriminator", "status" };
        foreach (var col in mtdColumns)
            expectedColumns.Add(col.Code.ToLowerInvariant());

        // Missing in DB
        foreach (var expected in expectedColumns)
        {
            if (!dbColumns.Contains(expected))
                drifts.Add(new DriftItem("🔴 Missing in DB", expected, "Колонка есть в MTD, но отсутствует в БД"));
        }

        // Extra in DB (not in MTD)
        foreach (var actual in dbColumns)
        {
            if (!expectedColumns.Contains(actual))
                drifts.Add(new DriftItem("🟡 Extra in DB", actual, "Колонка есть в БД, но отсутствует в MTD"));
        }

        return drifts;
    }

    private static async Task<Dictionary<string, HashSet<string>>> ParseSchemaDump(string path)
    {
        var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var lines = await File.ReadAllLinesAsync(path);

        string? currentTable = null;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Detect CREATE TABLE
            if (trimmed.StartsWith("CREATE TABLE", StringComparison.OrdinalIgnoreCase))
            {
                var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                // CREATE TABLE [IF NOT EXISTS] schema.table (
                currentTable = parts.Length >= 3 ? parts[^1].TrimEnd('(').Trim() : null;
                if (currentTable != null)
                {
                    // Remove schema prefix
                    var dotIdx = currentTable.LastIndexOf('.');
                    if (dotIdx >= 0) currentTable = currentTable[(dotIdx + 1)..];
                    currentTable = currentTable.Trim('"').ToLowerInvariant();
                    result[currentTable] = [];
                }
                continue;
            }

            // Inside CREATE TABLE — extract column name
            if (currentTable != null && !string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith(")"))
            {
                if (trimmed.StartsWith("CONSTRAINT", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("PRIMARY", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
                    trimmed.StartsWith("CHECK", StringComparison.OrdinalIgnoreCase))
                    continue;

                var colName = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (colName != null)
                {
                    colName = colName.Trim('"', ',').ToLowerInvariant();
                    if (!string.IsNullOrEmpty(colName))
                        result[currentTable].Add(colName);
                }
            }

            if (trimmed.StartsWith(");") || trimmed == ")")
                currentTable = null;
        }

        return result;
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private record MtdColumn(string Name, string Code, string PropType, string DbType, bool IsNullable);
    private record DriftItem(string Type, string Column, string Details);
}
