using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class MapDbSchemaTool
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
            if (!PathGuard.IsAllowed(entityMtdPath))
                return PathGuard.DenyMessage(entityMtdPath);
            if (!File.Exists(entityMtdPath))
                return $"**ОШИБКА**: Файл не найден: `{entityMtdPath}`";

            resolvedEntityMtdPath = entityMtdPath;
            // Derive module path: go up from Entity.mtd to module root
            // Typical: work/Module/Module.Shared/Entity/Entity.mtd → work/Module
            resolvedModulePath = DeduceModulePath(entityMtdPath);
        }
        else if (!string.IsNullOrWhiteSpace(modulePath) && !string.IsNullOrWhiteSpace(entityName))
        {
            if (!PathGuard.IsAllowed(modulePath))
                return PathGuard.DenyMessage(modulePath);
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
}
