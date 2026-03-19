using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class GenerateCrudApiTool
{
    private static readonly Dictionary<string, string> DbTypeMap = new(StringComparer.OrdinalIgnoreCase)
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

    private static readonly Dictionary<string, string> CSharpTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StringProperty"] = "string",
        ["IntegerProperty"] = "long",
        ["DoubleProperty"] = "double",
        ["BooleanProperty"] = "bool",
        ["DateTimeProperty"] = "DateTime",
        ["NavigationProperty"] = "long",
        ["EnumProperty"] = "string",
        ["TextProperty"] = "string",
    };

    private static readonly Dictionary<string, string> NpgsqlReaderMethod = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StringProperty"] = "GetString",
        ["IntegerProperty"] = "GetInt64",
        ["DoubleProperty"] = "GetDouble",
        ["BooleanProperty"] = "GetBoolean",
        ["DateTimeProperty"] = "GetDateTime",
        ["NavigationProperty"] = "GetInt64",
        ["EnumProperty"] = "GetString",
        ["TextProperty"] = "GetString",
    };

    [McpServerTool(Name = "generate_crud_api")]
    [Description("Генерирует C# код CRUD endpoints (GET/POST/PATCH/DELETE) для standalone .NET API на основе MTD метаданных сущности. Поддерживает два стиля: 'odata' (по умолчанию, через ODataService) и 'sql' (прямые SQL-запросы).")]
    public async Task<string> Execute(
        [Description("Путь к .mtd файлу сущности")] string entityMtdPath,
        [Description("Стиль генерации: 'odata' (по умолчанию, через ODataService) или 'sql' (прямые SQL-запросы)")] string? style = "odata")
    {
        if (!PathGuard.IsAllowed(entityMtdPath))
            return PathGuard.DenyMessage(entityMtdPath);
        if (!File.Exists(entityMtdPath))
            return $"**ОШИБКА**: Файл не найден: `{entityMtdPath}`";

        // Deduce module path and find Module.mtd
        var modulePath = DeduceModulePath(entityMtdPath);
        var moduleMtdPath = FindModuleMtd(modulePath);
        if (moduleMtdPath == null)
            return $"**ОШИБКА**: Module.mtd не найден в `{modulePath}`";

        // Parse Module.mtd
        string moduleCode;
        string moduleNamespace;
        using (var moduleDoc = await MtdParser.ParseRawAsync(moduleMtdPath))
        {
            moduleCode = GetString(moduleDoc.RootElement, "Code");
            moduleNamespace = GetString(moduleDoc.RootElement, "Name");
            if (string.IsNullOrEmpty(moduleCode))
                moduleCode = Path.GetFileName(modulePath);
        }

        // Parse Entity.mtd
        using var entityDoc = await MtdParser.ParseRawAsync(entityMtdPath);
        var root = entityDoc.RootElement;

        var entityName = GetString(root, "Name");
        var entityCode = GetString(root, "Code");
        if (string.IsNullOrEmpty(entityCode))
            entityCode = entityName;

        var prefix = DeterminePrefix(moduleNamespace, modulePath);
        var tableName = $"{prefix}_{moduleCode}_{entityCode}".ToLowerInvariant();
        var entityLower = entityName.ToLowerInvariant();
        var moduleCodeLower = moduleCode.ToLowerInvariant();

        // Parse properties (non-collection, non-ancestor)
        var props = new List<PropInfo>();
        if (root.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsEl.EnumerateArray())
            {
                var propType = GetString(prop, "$type");
                if (propType.Contains("CollectionPropertyMetadata"))
                    continue;

                var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var ancEl) && ancEl.ValueKind == JsonValueKind.True;
                if (isAncestor) continue;

                var pName = GetString(prop, "Name");
                var pCode = GetString(prop, "Code");
                if (string.IsNullOrEmpty(pCode)) pCode = pName;
                var isRequired = prop.TryGetProperty("IsRequired", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;

                var shortType = ExtractShortType(propType);
                var dbType = DbTypeMap.GetValueOrDefault(shortType, "citext");
                var csType = CSharpTypeMap.GetValueOrDefault(shortType, "string");
                var readerMethod = NpgsqlReaderMethod.GetValueOrDefault(shortType, "GetString");
                var isNav = shortType == "NavigationProperty";

                string? fkEntityGuid = null;
                if (isNav && prop.TryGetProperty("EntityGuid", out var egEl) && egEl.ValueKind == JsonValueKind.String)
                    fkEntityGuid = egEl.GetString();

                props.Add(new PropInfo(pName, pCode, shortType, dbType, csType, readerMethod, isRequired, isNav, fkEntityGuid));
            }
        }

        // Resolve FK table names from sibling entities
        var guidToTable = await BuildGuidToTableMap(modulePath, prefix, moduleCode);

        var sb = new StringBuilder();
        var useOData = !string.Equals(style, "sql", StringComparison.OrdinalIgnoreCase);
        var integrationName = $"I{moduleNamespace.Replace("DirRX.", "").Replace(".", "")}{entityName}s";

        sb.AppendLine($"## CRUD API для {entityName}");
        sb.AppendLine($"**Стиль**: {(useOData ? "OData (через ODataService)" : "SQL (прямые запросы)")}");
        if (useOData)
            sb.AppendLine($"**OData EntitySet**: `{integrationName}` (проверьте реальное имя через GET /Integration/odata/)");
        sb.AppendLine($"**Таблица БД**: `{tableName}`");
        sb.AppendLine();

        // ── DTO ──
        sb.AppendLine("### DTO");
        sb.AppendLine();
        sb.AppendLine("```csharp");
        RenderCreateDto(sb, entityName, props);
        sb.AppendLine();
        RenderUpdateDto(sb, entityName, props);
        sb.AppendLine("```");
        sb.AppendLine();

        if (useOData)
        {
            // ── OData Constants ──
            sb.AppendLine("### OData Constants (добавить в CrmConstants.ODataSets)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine($"public const string {entityName}s = \"{integrationName}\";");
            sb.AppendLine("```");
            sb.AppendLine();

            // ── GET via OData ──
            sb.AppendLine($"### GET /api/crm/{entityLower}?{entityLower}Id=N (OData)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            RenderODataGetEndpoint(sb, entityName, entityLower, integrationName, props);
            sb.AppendLine("```");
            sb.AppendLine();

            // ── CREATE via OData ──
            sb.AppendLine($"### POST /api/crm/{entityLower}/create (OData)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            RenderODataCreateEndpoint(sb, entityName, entityLower, integrationName, props);
            sb.AppendLine("```");
            sb.AppendLine();

            // ── UPDATE via OData ──
            sb.AppendLine($"### PATCH /api/crm/{entityLower}/update (OData)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            RenderODataUpdateEndpoint(sb, entityName, entityLower, integrationName, props);
            sb.AppendLine("```");
            sb.AppendLine();

            // ── DELETE via OData ──
            sb.AppendLine($"### DELETE /api/crm/{entityLower}/{{id}} (OData)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            sb.AppendLine($"group.MapDelete(\"/{entityLower}/{{id}}\", async (long id, ODataService odata) =>");
            sb.AppendLine("{");
            sb.AppendLine($"    var result = await odata.DeleteAsync(CrmConstants.ODataSets.{entityName}s, id);");
            sb.AppendLine("    if (!result.Success)");
            sb.AppendLine($"        return Results.Json(new ErrorResponse(\"Failed to delete {entityName}\", result.Error), statusCode: result.StatusCode);");
            sb.AppendLine($"    Log.Debug(\"{entityName} {{Id}} deleted\", id);");
            sb.AppendLine("    return Results.NoContent();");
            sb.AppendLine("});");
            sb.AppendLine("```");
        }
        else
        {
            // ── SQL-based endpoints (legacy) ──
            sb.AppendLine($"### GET /api/{moduleCodeLower}/{entityLower}?{entityLower}Id=N (SQL)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            RenderGetEndpoint(sb, entityName, entityLower, tableName, props, guidToTable);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine($"### POST /api/{moduleCodeLower}/{entityLower}/create (SQL)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            RenderCreateEndpoint(sb, entityName, entityLower, tableName, props);
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine($"### POST /api/{moduleCodeLower}/{entityLower}/update (SQL)");
            sb.AppendLine();
            sb.AppendLine("```csharp");
            RenderUpdateEndpoint(sb, entityName, entityLower, tableName, props);
            sb.AppendLine("```");
        }

        return sb.ToString();
    }

    private record PropInfo(
        string Name, string Code, string ShortType, string DbType,
        string CsType, string ReaderMethod, bool IsRequired,
        bool IsNavigation, string? FkEntityGuid);

    // ── DTO rendering ──────────────────────────────────────────────────────────

    private static void RenderCreateDto(StringBuilder sb, string entityName, List<PropInfo> props)
    {
        sb.Append($"public record Create{entityName}Request(");
        var parts = new List<string>();
        foreach (var p in props)
        {
            var csType = p.IsNavigation ? "long" : p.CsType;
            var paramName = p.IsNavigation ? $"{p.Name}Id" : p.Name;
            if (p.IsRequired)
                parts.Add($"{csType} {paramName}");
            else
                parts.Add($"{csType} {paramName} = {DefaultValue(p)}");
        }
        sb.Append(string.Join(", ", parts));
        sb.AppendLine(");");
    }

    private static void RenderUpdateDto(StringBuilder sb, string entityName, List<PropInfo> props)
    {
        sb.Append($"public record Update{entityName}Request(long {entityName}Id");
        foreach (var p in props)
        {
            var csType = p.IsNavigation ? "long?" : NullableType(p.CsType);
            var paramName = p.IsNavigation ? $"{p.Name}Id" : p.Name;
            sb.Append($", {csType} {paramName} = null");
        }
        sb.AppendLine(");");
    }

    // ── GET endpoint ───────────────────────────────────────────────────────────

    private static void RenderGetEndpoint(StringBuilder sb, string entityName, string entityLower,
        string tableName, List<PropInfo> props, Dictionary<string, string> guidToTable)
    {
        sb.AppendLine($"group.MapGet(\"/{entityLower}\", async (long {entityLower}Id, DbService db) =>");
        sb.AppendLine("{");
        sb.AppendLine("    await using var conn = await db.GetConnectionAsync();");

        // Build SELECT columns
        var selectCols = new List<string> { "t.id" };
        foreach (var p in props)
        {
            selectCols.Add($"t.{p.Code.ToLowerInvariant()}");
        }

        var selectStr = string.Join(", ", selectCols);

        sb.AppendLine($"    await using var cmd = new NpgsqlCommand(@\"");
        sb.AppendLine($"        SELECT {selectStr}");
        sb.AppendLine($"        FROM {tableName} t");
        sb.AppendLine($"        WHERE t.id = @id\", conn);");
        sb.AppendLine($"    cmd.Parameters.AddWithValue(\"id\", {entityLower}Id);");
        sb.AppendLine();
        sb.AppendLine("    await using var reader = await cmd.ExecuteReaderAsync();");
        sb.AppendLine("    if (!await reader.ReadAsync())");
        sb.AppendLine($"        return Results.NotFound(new {{ error = \"{entityName} not found\" }});");
        sb.AppendLine();
        sb.AppendLine("    return Results.Ok(new");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = reader.GetInt64(0),");

        for (int i = 0; i < props.Count; i++)
        {
            var p = props[i];
            var colIdx = i + 1;
            var comma = i < props.Count - 1 ? "," : "";
            sb.AppendLine($"        {p.Name} = reader.IsDBNull({colIdx}) ? null : reader.{p.ReaderMethod}({colIdx}){comma}");
        }

        sb.AppendLine("    });");
        sb.AppendLine("});");
    }

    // ── CREATE endpoint ────────────────────────────────────────────────────────

    private static void RenderCreateEndpoint(StringBuilder sb, string entityName, string entityLower,
        string tableName, List<PropInfo> props)
    {
        sb.AppendLine($"group.MapPost(\"/{entityLower}/create\", async (Create{entityName}Request req, DbService db) =>");
        sb.AppendLine("{");
        sb.AppendLine("    await using var conn = await db.GetConnectionAsync();");
        sb.AppendLine();
        sb.AppendLine("    // Generate next ID");
        sb.AppendLine($"    var idCmd = new NpgsqlCommand(\"SELECT COALESCE(MAX(id), 0) + 1 FROM {tableName}\", conn);");
        sb.AppendLine("    var newId = (long)(await idCmd.ExecuteScalarAsync())!;");
        sb.AppendLine();

        var colNames = new List<string> { "id", "discriminator", "status" };
        var paramNames = new List<string> { "@id", "@disc", "'Active'" };

        foreach (var p in props)
        {
            colNames.Add(p.Code.ToLowerInvariant());
            paramNames.Add($"@{p.Code.ToLowerInvariant()}");
        }

        sb.AppendLine($"    await using var cmd = new NpgsqlCommand(@\"");
        sb.AppendLine($"        INSERT INTO {tableName} ({string.Join(", ", colNames)})");
        sb.AppendLine($"        VALUES ({string.Join(", ", paramNames)})\", conn);");
        sb.AppendLine();
        sb.AppendLine("    cmd.Parameters.AddWithValue(\"id\", newId);");
        sb.AppendLine("    cmd.Parameters.AddWithValue(\"disc\", Guid.NewGuid());");

        foreach (var p in props)
        {
            var paramName = p.IsNavigation ? $"{p.Name}Id" : p.Name;
            sb.AppendLine($"    cmd.Parameters.AddWithValue(\"{p.Code.ToLowerInvariant()}\", req.{paramName});");
        }

        sb.AppendLine();
        sb.AppendLine("    await cmd.ExecuteNonQueryAsync();");
        sb.AppendLine($"    return Results.Created($\"/{entityLower}?{entityLower}Id={{newId}}\", new {{ Id = newId }});");
        sb.AppendLine("});");
    }

    // ── UPDATE endpoint ────────────────────────────────────────────────────────

    private static void RenderUpdateEndpoint(StringBuilder sb, string entityName, string entityLower,
        string tableName, List<PropInfo> props)
    {
        sb.AppendLine($"group.MapPost(\"/{entityLower}/update\", async (Update{entityName}Request req, DbService db) =>");
        sb.AppendLine("{");
        sb.AppendLine("    await using var conn = await db.GetConnectionAsync();");
        sb.AppendLine();
        sb.AppendLine("    var setClauses = new List<string>();");
        sb.AppendLine("    var cmd = new NpgsqlCommand();");
        sb.AppendLine("    cmd.Connection = conn;");
        sb.AppendLine();

        foreach (var p in props)
        {
            var paramName = p.IsNavigation ? $"{p.Name}Id" : p.Name;
            var colName = p.Code.ToLowerInvariant();
            sb.AppendLine($"    if (req.{paramName} != null)");
            sb.AppendLine("    {");
            sb.AppendLine($"        setClauses.Add(\"{colName} = @{colName}\");");
            sb.AppendLine($"        cmd.Parameters.AddWithValue(\"{colName}\", req.{paramName});");
            sb.AppendLine("    }");
            sb.AppendLine();
        }

        sb.AppendLine("    if (setClauses.Count == 0)");
        sb.AppendLine("        return Results.BadRequest(new { error = \"No fields to update\" });");
        sb.AppendLine();
        sb.AppendLine($"    cmd.CommandText = $\"UPDATE {tableName} SET {{string.Join(\", \", setClauses)}} WHERE id = @id\";");
        sb.AppendLine($"    cmd.Parameters.AddWithValue(\"id\", req.{entityName}Id);");
        sb.AppendLine();
        sb.AppendLine("    var affected = await cmd.ExecuteNonQueryAsync();");
        sb.AppendLine("    return affected > 0");
        sb.AppendLine($"        ? Results.Ok(new {{ updated = req.{entityName}Id }})");
        sb.AppendLine($"        : Results.NotFound(new {{ error = \"{entityName} not found\" }});");
        sb.AppendLine("});");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    private static string DefaultValue(PropInfo p) => p.ShortType switch
    {
        "StringProperty" or "TextProperty" or "EnumProperty" => "\"\"",
        "IntegerProperty" or "NavigationProperty" => "0",
        "DoubleProperty" => "0",
        "BooleanProperty" => "false",
        "DateTimeProperty" => "default",
        _ => "default"
    };

    private static string NullableType(string csType) => csType switch
    {
        "string" => "string?",
        "long" => "long?",
        "double" => "double?",
        "bool" => "bool?",
        "DateTime" => "DateTime?",
        _ => $"{csType}?"
    };

    private static string DeterminePrefix(string moduleNamespace, string modulePath)
    {
        if (moduleNamespace.StartsWith("DirRX", StringComparison.OrdinalIgnoreCase))
            return "dirrx";
        if (moduleNamespace.StartsWith("Sungero", StringComparison.OrdinalIgnoreCase))
            return "sungero";

        var dirName = Path.GetFileName(modulePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? "";
        if (dirName.StartsWith("DirRX", StringComparison.OrdinalIgnoreCase))
            return "dirrx";

        return "sungero";
    }

    private static string DeduceModulePath(string entityMtdPath)
    {
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

    private static string? FindModuleMtd(string modulePath)
    {
        var candidates = Directory.GetFiles(modulePath, "Module.mtd", SearchOption.AllDirectories);
        return candidates.FirstOrDefault();
    }

    private static async Task<Dictionary<string, string>> BuildGuidToTableMap(string modulePath, string prefix, string moduleCode)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entityMtds = Directory.GetFiles(modulePath, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase));

        foreach (var mtdFile in entityMtds)
        {
            try
            {
                using var doc = await MtdParser.ParseRawAsync(mtdFile);
                var r = doc.RootElement;
                var metaType = GetString(r, "$type");
                if (!metaType.Contains("EntityMetadata")) continue;

                var guid = GetString(r, "NameGuid");
                var code = GetString(r, "Code");
                var name = GetString(r, "Name");
                if (string.IsNullOrEmpty(code)) code = name;
                if (!string.IsNullOrEmpty(guid) && !string.IsNullOrEmpty(code))
                    map[guid] = $"{prefix}_{moduleCode}_{code}".ToLowerInvariant();
            }
            catch { }
        }
        return map;
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

    // ── OData GET endpoint ──────────────────────────────────────────────────────
    private static void RenderODataGetEndpoint(StringBuilder sb, string entityName, string entityLower,
        string integrationName, List<PropInfo> props)
    {
        var navProps = props.Where(p => p.IsNavigation).ToList();
        var expandClause = navProps.Count > 0
            ? $"$expand={string.Join(",", navProps.Select(p => p.Name))}&"
            : "";

        sb.AppendLine($"group.MapGet(\"/{entityLower}\", async (long {entityLower}Id, ODataService odata) =>");
        sb.AppendLine("{");
        sb.AppendLine($"    var result = await odata.GetAsync<JsonElement>($\"{{CrmConstants.ODataSets.{entityName}s}}({{{entityLower}Id}})\", \"{expandClause}$select=Id,{string.Join(",", props.Select(p => p.Name))}\");");
        sb.AppendLine("    if (!result.Success)");
        sb.AppendLine($"        return Results.Json(new ErrorResponse(\"{entityName} not found\", result.Error), statusCode: result.StatusCode);");
        sb.AppendLine();
        sb.AppendLine("    var el = result.Data;");
        sb.AppendLine("    return Results.Ok(new");
        sb.AppendLine("    {");
        sb.AppendLine("        Id = el.GetProperty(\"Id\").GetInt64(),");

        foreach (var p in props)
        {
            if (p.IsNavigation)
            {
                sb.AppendLine($"        {p.Name}Id = el.TryGetProperty(\"{p.Name}\", out var {p.Name.ToLowerInvariant()}) && {p.Name.ToLowerInvariant()}.ValueKind != JsonValueKind.Null && {p.Name.ToLowerInvariant()}.TryGetProperty(\"Id\", out var {p.Name.ToLowerInvariant()}Id) ? {p.Name.ToLowerInvariant()}Id.GetInt64() : (long?)null,");
            }
            else
            {
                var getter = p.ShortType switch
                {
                    "IntegerProperty" => $"el.TryGetProperty(\"{p.Name}\", out var {p.Name.ToLowerInvariant()}) && {p.Name.ToLowerInvariant()}.ValueKind == JsonValueKind.Number ? {p.Name.ToLowerInvariant()}.GetInt64() : 0",
                    "DoubleProperty" => $"el.TryGetProperty(\"{p.Name}\", out var {p.Name.ToLowerInvariant()}) && {p.Name.ToLowerInvariant()}.ValueKind == JsonValueKind.Number ? {p.Name.ToLowerInvariant()}.GetDouble() : 0",
                    "BooleanProperty" => $"el.TryGetProperty(\"{p.Name}\", out var {p.Name.ToLowerInvariant()}) && {p.Name.ToLowerInvariant()}.ValueKind == JsonValueKind.True",
                    _ => $"el.TryGetProperty(\"{p.Name}\", out var {p.Name.ToLowerInvariant()}) ? {p.Name.ToLowerInvariant()}.GetString() ?? \"\" : \"\""
                };
                sb.AppendLine($"        {p.Name} = {getter},");
            }
        }

        sb.AppendLine("    });");
        sb.AppendLine("});");
    }

    // ── OData CREATE endpoint ───────────────────────────────────────────────────
    private static void RenderODataCreateEndpoint(StringBuilder sb, string entityName, string entityLower,
        string integrationName, List<PropInfo> props)
    {
        sb.AppendLine($"group.MapPost(\"/{entityLower}/create\", async (Create{entityName}Request req, ODataService odata) =>");
        sb.AppendLine("{");
        sb.AppendLine("    var payload = new Dictionary<string, object?>");
        sb.AppendLine("    {");

        foreach (var p in props.Where(p => !p.IsNavigation))
        {
            var paramName = p.Name;
            sb.AppendLine($"        [\"{p.Name}\"] = req.{paramName},");
        }

        sb.AppendLine("    };");
        sb.AppendLine();

        foreach (var p in props.Where(p => p.IsNavigation))
        {
            sb.AppendLine($"    if (req.{p.Name}Id > 0)");
            sb.AppendLine($"        payload[\"{p.Name}\"] = new ODataBind(CrmConstants.ODataSets./* TODO: {p.Name} EntitySet */, req.{p.Name}Id);");
        }

        sb.AppendLine();
        sb.AppendLine($"    var result = await odata.PostWithBindAsync<JsonElement>(CrmConstants.ODataSets.{entityName}s, payload);");
        sb.AppendLine("    if (!result.Success)");
        sb.AppendLine($"        return Results.Json(new ErrorResponse(\"Failed to create {entityName}\", result.Error), statusCode: result.StatusCode);");
        sb.AppendLine();
        sb.AppendLine("    var id = result.Data.GetProperty(\"Id\").GetInt64();");
        sb.AppendLine($"    Log.Debug(\"{entityName} {{Id}} created\", id);");
        sb.AppendLine("    return Results.Ok(new SuccessIdResponse(true, id));");
        sb.AppendLine("});");
    }

    // ── OData UPDATE endpoint ───────────────────────────────────────────────────
    private static void RenderODataUpdateEndpoint(StringBuilder sb, string entityName, string entityLower,
        string integrationName, List<PropInfo> props)
    {
        sb.AppendLine($"group.MapPost(\"/{entityLower}/update\", async (Update{entityName}Request req, ODataService odata) =>");
        sb.AppendLine("{");
        sb.AppendLine("    var payload = new Dictionary<string, object?>();");
        sb.AppendLine();

        foreach (var p in props.Where(p => !p.IsNavigation))
        {
            var paramName = p.Name;
            sb.AppendLine($"    if (req.{paramName} != null) payload[\"{p.Name}\"] = req.{paramName};");
        }

        foreach (var p in props.Where(p => p.IsNavigation))
        {
            sb.AppendLine($"    if (req.{p.Name}Id.HasValue && req.{p.Name}Id.Value > 0)");
            sb.AppendLine($"        payload[\"{p.Name}\"] = new ODataBind(CrmConstants.ODataSets./* TODO: {p.Name} EntitySet */, req.{p.Name}Id.Value);");
        }

        sb.AppendLine();
        sb.AppendLine("    if (payload.Count == 0)");
        sb.AppendLine("        return Results.BadRequest(new ErrorResponse(\"No fields to update\"));");
        sb.AppendLine();
        sb.AppendLine($"    var result = await odata.PatchWithBindAsync(CrmConstants.ODataSets.{entityName}s, req.{entityName}Id, payload);");
        sb.AppendLine("    if (!result.Success)");
        sb.AppendLine($"        return Results.Json(new ErrorResponse(\"Failed to update {entityName}\", result.Error), statusCode: result.StatusCode);");
        sb.AppendLine();
        sb.AppendLine($"    Log.Debug(\"{entityName} {{Id}} updated\", req.{entityName}Id);");
        sb.AppendLine($"    return Results.Ok(new SuccessIdResponse(true, req.{entityName}Id));");
        sb.AppendLine("});");
    }
}
