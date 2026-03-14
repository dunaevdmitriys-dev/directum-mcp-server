using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;
using DirectumMcp.Core.Helpers;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ExtractEntitySchemaTool
{
    private static readonly Dictionary<string, string> KnownBaseGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["04581d26-0780-4cfd-b3cd-c2cafc5798b0"] = "DatabookEntry",
        ["58cca102-1e97-4f07-b6ac-fd866a8b7cb1"] = "Document",
        ["d795d1f6-45c1-4e5e-9677-b53fb7280c7e"] = "Task",
        ["91cbfdc8-5d5d-465e-95a4-3a987e1a0c24"] = "Assignment",
        ["4e09273f-8b3a-489e-814e-a4ebfbba3e6c"] = "Notice",
    };

    [McpServerTool(Name = "extract_entity_schema")]
    [Description("Извлекает компактную семантическую схему сущности из .mtd файла: свойства с типами, обязательные поля, перечисления, навигационные свойства, коллекции, действия и группы вложений.")]
    public async Task<string> ExtractEntitySchema(
        [Description("Путь к .mtd файлу сущности")] string path,
        [Description("Формат вывода: 'markdown' (по умолчанию) или 'json-schema'")] string? format = "markdown",
        [Description("Включать ли унаследованные свойства (IsAncestorMetadata=true). По умолчанию false.")] bool? includeInherited = false)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        if (!File.Exists(path))
            return $"**ОШИБКА**: Файл не найден: `{path}`";

        var ext = Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".mtd")
            return $"**ОШИБКА**: Ожидается файл с расширением .mtd, получен: `{ext}`";

        var json = await File.ReadAllTextAsync(path);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            return $"**ОШИБКА**: Не удалось разобрать JSON: {ex.Message}";
        }

        using (doc)
        {
            var root = doc.RootElement;
            var metaType = GetString(root, "$type");

            if (metaType.Contains("ModuleMetadata"))
                return $"**ОШИБКА**: Файл является метаданными модуля, а не сущности. Используйте `inspect` для просмотра модуля.";

            var normalizedFormat = (format ?? "markdown").ToLowerInvariant().Trim();
            var includeInheritedVal = includeInherited ?? false;

            var schema = ParseEntitySchema(root, includeInheritedVal);

            return normalizedFormat switch
            {
                "json-schema" => RenderJsonSchema(schema),
                "markdown" => RenderMarkdown(schema),
                _ => $"**ОШИБКА**: Неизвестный формат `{format}`. Допустимые значения: `markdown`, `json-schema`."
            };
        }
    }

    // ─── Schema model ───────────────────────────────────────────────────────────

    private record PropertySchema(
        string Name,
        string Type,
        string? Code,
        bool IsRequired,
        bool IsInherited,
        string? NavigationTarget,
        List<string>? EnumValues
    );

    private record CollectionSchema(string Name, string? TargetEntity);

    private record ActionSchema(string Name, bool IsInherited);

    private record AttachmentGroupSchema(string Name, List<string> Constraints);

    private record EntitySchema(
        string Name,
        string NameGuid,
        string BaseGuid,
        string EntityKind,
        bool IsAbstract,
        List<PropertySchema> Properties,
        List<CollectionSchema> Collections,
        List<ActionSchema> Actions,
        List<AttachmentGroupSchema> AttachmentGroups
    );

    // ─── Parsing ────────────────────────────────────────────────────────────────

    private static EntitySchema ParseEntitySchema(JsonElement root, bool includeInherited)
    {
        var name = GetString(root, "Name");
        var nameGuid = GetString(root, "NameGuid");
        var baseGuid = GetString(root, "BaseGuid");
        var metaType = GetString(root, "$type");
        var isAbstract = root.TryGetProperty("IsAbstract", out var abs) && abs.ValueKind == JsonValueKind.True;

        var entityKind = metaType switch
        {
            var t when t.Contains("TaskMetadata") => "Task",
            var t when t.Contains("AssignmentMetadata") => "Assignment",
            var t when t.Contains("NoticeMetadata") => "Notice",
            var t when t.Contains("ReportMetadata") => "Report",
            _ => ResolveKindFromBase(baseGuid)
        };

        var properties = new List<PropertySchema>();
        var collections = new List<CollectionSchema>();

        if (root.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsEl.EnumerateArray())
            {
                var propType = GetString(prop, "$type");
                var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var ancEl) && ancEl.ValueKind == JsonValueKind.True;

                if (isAncestor && !includeInherited)
                    continue;

                if (propType.Contains("CollectionPropertyMetadata"))
                {
                    var colName = GetString(prop, "Name");
                    string? target = null;
                    if (prop.TryGetProperty("EntityGuid", out var egEl) && egEl.ValueKind == JsonValueKind.String)
                        target = egEl.GetString();
                    collections.Add(new CollectionSchema(colName, target));
                    continue;
                }

                var pName = GetString(prop, "Name");
                var pCode = prop.TryGetProperty("Code", out var codeEl) && codeEl.ValueKind == JsonValueKind.String
                    ? codeEl.GetString()
                    : null;
                var isRequired = prop.TryGetProperty("IsRequired", out var reqEl) && reqEl.ValueKind == JsonValueKind.True;

                var shortType = ExtractPropertyType(propType);

                string? navTarget = null;
                if (prop.TryGetProperty("EntityGuid", out var navEg) && navEg.ValueKind == JsonValueKind.String)
                    navTarget = navEg.GetString();

                List<string>? enumValues = null;
                if (prop.TryGetProperty("DirectValues", out var dvEl) && dvEl.ValueKind == JsonValueKind.Array)
                {
                    enumValues = dvEl.EnumerateArray()
                        .Select(v => GetString(v, "Name"))
                        .Where(v => !string.IsNullOrEmpty(v))
                        .ToList();
                }

                properties.Add(new PropertySchema(pName, shortType, pCode, isRequired, isAncestor, navTarget, enumValues));
            }
        }

        var actions = new List<ActionSchema>();
        if (root.TryGetProperty("Actions", out var actsEl) && actsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var action in actsEl.EnumerateArray())
            {
                var aName = GetString(action, "Name");
                var isAncestor = action.TryGetProperty("IsAncestorMetadata", out var ancEl) && ancEl.ValueKind == JsonValueKind.True;

                if (isAncestor && !includeInherited)
                    continue;

                actions.Add(new ActionSchema(aName, isAncestor));
            }
        }

        var attachmentGroups = new List<AttachmentGroupSchema>();
        if (root.TryGetProperty("AttachmentGroups", out var groupsEl) && groupsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in groupsEl.EnumerateArray())
            {
                var gName = GetString(group, "Name");
                var constraints = new List<string>();
                if (group.TryGetProperty("Constraints", out var cEl) && cEl.ValueKind == JsonValueKind.Array)
                {
                    constraints = cEl.EnumerateArray()
                        .Select(c => GetString(c, "Name"))
                        .Where(n => !string.IsNullOrEmpty(n))
                        .ToList();
                }
                attachmentGroups.Add(new AttachmentGroupSchema(gName, constraints));
            }
        }

        return new EntitySchema(name, nameGuid, baseGuid, entityKind, isAbstract,
            properties, collections, actions, attachmentGroups);
    }

    // ─── Markdown renderer ──────────────────────────────────────────────────────

    private static string RenderMarkdown(EntitySchema schema)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"## Схема сущности: {schema.Name}");
        sb.AppendLine();
        sb.AppendLine("| Поле | Значение |");
        sb.AppendLine("|------|----------|");
        sb.AppendLine($"| GUID | `{schema.NameGuid}` |");
        sb.AppendLine($"| Тип | {schema.EntityKind} |");
        sb.AppendLine($"| Базовый GUID | `{schema.BaseGuid}` |");
        if (schema.IsAbstract)
            sb.AppendLine("| Абстрактный | да |");
        sb.AppendLine();

        // Properties
        if (schema.Properties.Count > 0)
        {
            sb.AppendLine($"### Свойства ({schema.Properties.Count})");
            sb.AppendLine();
            sb.AppendLine("| Свойство | Тип | Code | Обязательное | Детали |");
            sb.AppendLine("|----------|-----|------|-------------|--------|");

            foreach (var p in schema.Properties)
            {
                var details = new List<string>();
                if (p.IsInherited) details.Add("унаследовано");
                if (p.NavigationTarget != null) details.Add($"-> `{p.NavigationTarget}`");
                if (p.EnumValues != null && p.EnumValues.Count > 0)
                    details.Add($"значения: [{string.Join(", ", p.EnumValues)}]");

                sb.AppendLine($"| {p.Name} | {p.Type} | {p.Code ?? ""} | {(p.IsRequired ? "да" : "")} | {string.Join("; ", details)} |");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Свойства");
            sb.AppendLine();
            sb.AppendLine("_Свойства отсутствуют._");
            sb.AppendLine();
        }

        // Collections
        if (schema.Collections.Count > 0)
        {
            sb.AppendLine($"### Коллекции ({schema.Collections.Count})");
            sb.AppendLine();
            sb.AppendLine("| Коллекция | Целевая сущность |");
            sb.AppendLine("|-----------|-----------------|");
            foreach (var c in schema.Collections)
                sb.AppendLine($"| {c.Name} | {(c.TargetEntity != null ? $"`{c.TargetEntity}`" : "")} |");
            sb.AppendLine();
        }

        // Actions
        if (schema.Actions.Count > 0)
        {
            sb.AppendLine($"### Действия ({schema.Actions.Count})");
            sb.AppendLine();
            sb.AppendLine("| Действие | Унаследовано |");
            sb.AppendLine("|----------|-------------|");
            foreach (var a in schema.Actions)
                sb.AppendLine($"| {a.Name} | {(a.IsInherited ? "да" : "нет")} |");
            sb.AppendLine();
        }

        // Attachment groups
        if (schema.AttachmentGroups.Count > 0)
        {
            sb.AppendLine($"### Группы вложений ({schema.AttachmentGroups.Count})");
            sb.AppendLine();
            sb.AppendLine("| Группа | Ограничения |");
            sb.AppendLine("|--------|-------------|");
            foreach (var g in schema.AttachmentGroups)
            {
                var constraintStr = g.Constraints.Count > 0
                    ? string.Join(", ", g.Constraints)
                    : "_нет_";
                sb.AppendLine($"| {g.Name} | {constraintStr} |");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ─── JSON Schema renderer ───────────────────────────────────────────────────

    private static string RenderJsonSchema(EntitySchema schema)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var p in schema.Properties)
        {
            var propDef = BuildJsonSchemaProp(p);
            properties[p.Name] = propDef;
            if (p.IsRequired)
                required.Add(p.Name);
        }

        var schemaObj = new Dictionary<string, object>
        {
            ["$schema"] = "http://json-schema.org/draft-07/schema#",
            ["title"] = schema.Name,
            ["description"] = $"Схема сущности {schema.EntityKind} — {schema.Name} (GUID: {schema.NameGuid})",
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
            schemaObj["required"] = required;

        if (schema.Collections.Count > 0)
        {
            var colDefs = new Dictionary<string, object>();
            foreach (var col in schema.Collections)
            {
                var colDef = new Dictionary<string, object>
                {
                    ["type"] = "array",
                    ["items"] = new Dictionary<string, object> { ["type"] = "object" }
                };
                if (col.TargetEntity != null)
                    colDef["description"] = $"Коллекция -> {col.TargetEntity}";
                colDefs[col.Name] = colDef;
            }
            schemaObj["collections"] = colDefs;
        }

        if (schema.Actions.Count > 0)
        {
            schemaObj["x-actions"] = schema.Actions
                .Select(a => new Dictionary<string, object>
                {
                    ["name"] = a.Name,
                    ["inherited"] = a.IsInherited
                })
                .ToList();
        }

        if (schema.AttachmentGroups.Count > 0)
        {
            schemaObj["x-attachmentGroups"] = schema.AttachmentGroups
                .Select(g => new Dictionary<string, object>
                {
                    ["name"] = g.Name,
                    ["constraints"] = g.Constraints
                })
                .ToList();
        }

        return JsonSerializer.Serialize(schemaObj, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
    }

    private static Dictionary<string, object> BuildJsonSchemaProp(PropertySchema p)
    {
        var def = new Dictionary<string, object>();

        switch (p.Type)
        {
            case "String":
                def["type"] = "string";
                break;
            case "Integer" or "Int":
                def["type"] = "integer";
                break;
            case "Double" or "Numeric":
                def["type"] = "number";
                break;
            case "Bool" or "Boolean":
                def["type"] = "boolean";
                break;
            case "Date" or "DateTime":
                def["type"] = "string";
                def["format"] = "date-time";
                break;
            case "Enum":
                if (p.EnumValues != null && p.EnumValues.Count > 0)
                {
                    def["type"] = "string";
                    def["enum"] = p.EnumValues;
                }
                else
                {
                    def["type"] = "string";
                }
                break;
            case "Navigation":
                def["type"] = "object";
                if (p.NavigationTarget != null)
                    def["description"] = $"Ссылка на сущность с GUID {p.NavigationTarget}";
                break;
            default:
                def["type"] = "string";
                break;
        }

        if (p.Code != null && p.Code != p.Name)
            def["x-code"] = p.Code;

        if (p.IsInherited)
            def["x-inherited"] = true;

        return def;
    }

    // ─── Helpers ────────────────────────────────────────────────────────────────

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private static string ExtractPropertyType(string fullType)
    {
        var className = fullType.Split(',')[0].Split('.').LastOrDefault() ?? fullType;
        return className
            .Replace("PropertyMetadata", "")
            .Replace("Metadata", "");
    }

    private static string ResolveKindFromBase(string baseGuid)
    {
        return KnownBaseGuids.TryGetValue(baseGuid, out var kind) ? kind : "Entity";
    }
}
