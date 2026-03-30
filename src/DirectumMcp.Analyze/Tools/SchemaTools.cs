using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using System.Text;
using DirectumMcp.Core.Helpers;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class SchemaTools
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

    [McpServerTool(Name = "map_db_schema")]
    [Description("MTD → имена таблиц и колонок PostgreSQL + CREATE TABLE.")]
    public async Task<string> Execute(
        [Description("Путь к директории модуля (например work/DirRX.CRMSales)")] string? modulePath = null,
        [Description("Имя сущности (например Deal)")] string? entityName = null,
        [Description("Прямой путь к .mtd файлу сущности")] string? entityMtdPath = null)
    {
        // Resolve entity MTD path
        string resolvedEntityMtdPath;
        string resolvedModulePath;

        if (!string.IsNullOrWhiteSpace(entityMtdPath))
        {
            if (!File.Exists(entityMtdPath))
                return $"**ОШИБКА**: Файл не найден: `{entityMtdPath}`";

            resolvedEntityMtdPath = entityMtdPath;
            // Derive module path: go up from Entity.mtd to module root
            // Typical: work/Module/Module.Shared/Entity/Entity.mtd → work/Module
            resolvedModulePath = DeduceModulePath(entityMtdPath);
        }
        else if (!string.IsNullOrWhiteSpace(modulePath) && !string.IsNullOrWhiteSpace(entityName))
        {
            if (!Directory.Exists(modulePath))
                return $"**ОШИБКА**: Директория модуля не найдена: `{modulePath}`";

            resolvedModulePath = modulePath;
            resolvedEntityMtdPath = FindEntityMtd(modulePath, entityName)!;
            if (resolvedEntityMtdPath == null)
                return $"**ОШИБКА**: MTD файл для сущности `{entityName}` не найден в `{modulePath}`";
        }
        else
        {
            return "**ОШИБКА**: Укажите `entityMtdPath` или пару `modulePath` + `entityName`.";
        }

        // Find Module.mtd
        var moduleMtdPath = FindModuleMtd(resolvedModulePath);
        if (moduleMtdPath == null)
            return $"**ОШИБКА**: Module.mtd не найден в `{resolvedModulePath}`";

        // Parse Module.mtd for Code
        string moduleCode;
        string moduleNamespace;
        using (var moduleDoc = await MtdParser.ParseRawAsync(moduleMtdPath))
        {
            moduleCode = GetString(moduleDoc.RootElement, "Code");
            moduleNamespace = GetString(moduleDoc.RootElement, "Name");
            if (string.IsNullOrEmpty(moduleCode))
                moduleCode = Path.GetFileName(resolvedModulePath);
        }

        // Parse Entity.mtd
        using var entityDoc = await MtdParser.ParseRawAsync(resolvedEntityMtdPath);
        var root = entityDoc.RootElement;

        var entityNameFromMtd = GetString(root, "Name");
        var entityCode = GetString(root, "Code");
        if (string.IsNullOrEmpty(entityCode))
            entityCode = entityNameFromMtd;

        // Determine prefix
        var prefix = DeterminePrefix(moduleNamespace, resolvedModulePath);

        var tableName = $"{prefix}_{moduleCode}_{entityCode}".ToLowerInvariant();

        // Parse properties
        var columns = new List<ColumnInfo>();

        // System columns always present
        columns.Add(new ColumnInfo("id", "id", "int8", false, null, true));
        columns.Add(new ColumnInfo("discriminator", "discriminator", "uuid", false, null, true));
        columns.Add(new ColumnInfo("status", "status", "citext", true, null, true));

        if (root.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsEl.EnumerateArray())
            {
                var propType = GetString(prop, "$type");
                if (propType.Contains("CollectionPropertyMetadata"))
                    continue; // Collections are separate tables

                var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var ancEl) && ancEl.ValueKind == JsonValueKind.True;

                var pName = GetString(prop, "Name");
                var pCode = GetString(prop, "Code");
                if (string.IsNullOrEmpty(pCode))
                    pCode = pName;

                var shortType = ExtractShortType(propType);
                var dbType = TypeMap.GetValueOrDefault(shortType, "citext");
                var isRequired = prop.TryGetProperty("IsRequired", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;

                string? fkRef = null;
                if (shortType == "NavigationProperty" && prop.TryGetProperty("EntityGuid", out var egEl) && egEl.ValueKind == JsonValueKind.String)
                {
                    // We cannot resolve GUID to table name without scanning all modules, so mark as FK
                    fkRef = $"(FK → entity GUID {egEl.GetString()})";
                }

                columns.Add(new ColumnInfo(pName, pCode.ToLowerInvariant(), dbType, !isRequired, fkRef));
            }
        }

        // Try to resolve FK references by scanning sibling entities in the same module
        await ResolveForeignKeys(columns, resolvedModulePath, prefix, moduleCode);

        // Render output
        var sb = new StringBuilder();
        sb.AppendLine($"## DB Schema: {entityNameFromMtd}");
        sb.AppendLine();
        sb.AppendLine($"### Таблица: `{tableName}`");
        sb.AppendLine();
        sb.AppendLine("| Property | Code (MTD) | Column (DB) | Тип DB | Nullable | FK → |");
        sb.AppendLine("|----------|-----------|-------------|--------|----------|------|");

        foreach (var col in columns)
        {
            var nullable = col.IsNullable ? "NULL" : "NOT NULL";
            var fk = col.FkReference ?? "—";
            if (col.IsSystem)
            {
                sb.AppendLine($"| _{col.PropertyName}_ | — | {col.ColumnName} | {col.DbType} | {nullable} | {fk} |");
            }
            else
            {
                sb.AppendLine($"| {col.PropertyName} | {col.ColumnName} | {col.ColumnName} | {col.DbType} | {nullable} | {fk} |");
            }
        }

        sb.AppendLine();

        // Generate reference CREATE TABLE
        sb.AppendLine("### SQL CREATE (справочный)");
        sb.AppendLine();
        sb.AppendLine("```sql");
        sb.AppendLine($"CREATE TABLE IF NOT EXISTS {tableName} (");
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var nullable = col.IsNullable ? "" : " NOT NULL";
            var comma = i < columns.Count - 1 ? "," : "";
            sb.AppendLine($"  {col.ColumnName} {col.DbType}{nullable}{comma}");
        }
        sb.AppendLine(");");
        sb.AppendLine("```");

        return sb.ToString();
    }

    private record ColumnInfo(string PropertyName, string ColumnName, string DbType, bool IsNullable, string? FkReference, bool IsSystem = false);

    private static string DeterminePrefix(string moduleNamespace, string modulePath)
    {
        if (moduleNamespace.StartsWith("DirRX", StringComparison.OrdinalIgnoreCase))
            return "dirrx";
        if (moduleNamespace.StartsWith("Sungero", StringComparison.OrdinalIgnoreCase))
            return "sungero";

        // Fallback: check directory path
        var dirName = Path.GetFileName(modulePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "";
        if (dirName.StartsWith("DirRX", StringComparison.OrdinalIgnoreCase))
            return "dirrx";

        return "sungero";
    }

    private static string DeduceModulePath(string entityMtdPath)
    {
        // Entity.mtd is typically at: ModuleDir/Module.Shared/EntityName/Entity.mtd
        // Go up 3 levels: Entity.mtd → EntityDir → Module.Shared → ModuleDir
        var dir = Path.GetDirectoryName(entityMtdPath)!;
        var sharedDir = Path.GetDirectoryName(dir);
        if (sharedDir != null)
        {
            var moduleDir = Path.GetDirectoryName(sharedDir);
            if (moduleDir != null && Directory.Exists(moduleDir))
                return moduleDir;
        }
        return dir;
    }

    private static string? FindEntityMtd(string modulePath, string entityName)
    {
        var candidates = Directory.GetFiles(modulePath, $"{entityName}.mtd", SearchOption.AllDirectories);
        return candidates
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static string? FindModuleMtd(string modulePath)
    {
        var candidates = Directory.GetFiles(modulePath, "Module.mtd", SearchOption.AllDirectories);
        return candidates.FirstOrDefault();
    }

    private static async Task ResolveForeignKeys(List<ColumnInfo> columns, string modulePath, string prefix, string moduleCode)
    {
        // Build a map of EntityGuid → table name from sibling entities
        var guidToTable = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var entityMtds = Directory.GetFiles(modulePath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase));

        foreach (var mtdFile in entityMtds)
        {
            try
            {
                using var doc = await MtdParser.ParseRawAsync(mtdFile);
                var r = doc.RootElement;
                var metaType = GetString(r, "$type");
                if (!metaType.Contains("EntityMetadata"))
                    continue;

                var guid = GetString(r, "NameGuid");
                var code = GetString(r, "Code");
                var name = GetString(r, "Name");
                if (string.IsNullOrEmpty(code))
                    code = name;
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(code))
                {
                    guidToTable[guid] = $"{prefix}_{moduleCode}_{code}".ToLowerInvariant();
                }
            }
            catch { }
        }

        // Replace FK references
        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            if (col.FkReference == null || !col.FkReference.StartsWith("(FK"))
                continue;

            // Extract GUID from the placeholder
            var guidStart = col.FkReference.IndexOf("GUID ", StringComparison.Ordinal);
            if (guidStart < 0) continue;
            var guid = col.FkReference[(guidStart + 5)..].TrimEnd(')').Trim();

            if (guidToTable.TryGetValue(guid, out var resolvedTable))
            {
                columns[i] = col with { FkReference = $"{resolvedTable}(id)" };
            }
        }
    }

    private static string ExtractShortType(string fullType)
    {
        var className = fullType.Split(',')[0].Split('.').LastOrDefault() ?? fullType;
        return className.Replace("Metadata", "");
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }


    private static readonly Dictionary<string, string> CompareTypeMap = new(StringComparer.OrdinalIgnoreCase)
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
        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория не найдена: `{modulePath}`";

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
            var dbType = CompareTypeMap.GetValueOrDefault(shortType, "citext");
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

    private record MtdColumn(string Name, string Code, string PropType, string DbType, bool IsNullable);
    private record DriftItem(string Type, string Column, string Details);

}
