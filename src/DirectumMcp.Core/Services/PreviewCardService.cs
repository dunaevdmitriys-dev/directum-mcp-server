using System.Text;
using System.Text.Json;
using System.Xml.Linq;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Generates text preview of entity card from .mtd file.
/// </summary>
public partial class PreviewCardService : IPipelineStep
{
    public string ToolName => "preview_card";

    public async Task<PreviewCardResult> PreviewAsync(
        string entityPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityPath))
            return Fail("Параметр `entityPath` не может быть пустым.");

        // Find .mtd file
        string mtdPath;
        if (File.Exists(entityPath) && entityPath.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
        {
            mtdPath = entityPath;
        }
        else if (Directory.Exists(entityPath))
        {
            var candidates = Directory.GetFiles(entityPath, "*.mtd", SearchOption.TopDirectoryOnly)
                .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (candidates.Length == 0)
                return Fail($"В директории `{entityPath}` не найдены .mtd файлы сущностей.");
            mtdPath = candidates[0];
        }
        else
        {
            return Fail($"Путь не найден: `{entityPath}`");
        }

        var json = await File.ReadAllTextAsync(mtdPath, ct);
        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (Exception ex) { return Fail($"Ошибка парсинга JSON: {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var entityName = root.GetStringPropSafe("Name");
            var baseGuid = root.GetStringPropSafe("BaseGuid");
            var baseType = DirectumMcp.Core.Helpers.DirectumConstants.ResolveBaseType(baseGuid);

            // Parse properties
            var properties = new List<PropertyPreview>();
            if (root.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in propsEl.EnumerateArray())
                {
                    var propType = prop.GetStringPropSafe("$type");
                    var shortType = propType.Contains('.') ? propType.Split('.').Last().Replace("Metadata", "").Replace(", Sungero", "") : propType;
                    var isRequired = prop.TryGetProperty("IsRequired", out var req) && req.GetBoolean();

                    properties.Add(new PropertyPreview(
                        prop.GetStringPropSafe("Name"),
                        shortType,
                        prop.GetStringPropSafe("NameGuid"),
                        isRequired,
                        prop.TryGetProperty("IsAncestorMetadata", out var anc) && anc.GetBoolean()
                    ));
                }
            }

            // Parse forms/controls
            var controlGroups = new List<ControlGroupPreview>();
            if (root.TryGetProperty("Forms", out var formsEl) && formsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var form in formsEl.EnumerateArray())
                {
                    if (!form.TryGetProperty("Controls", out var controls) || controls.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var ctrl in controls.EnumerateArray())
                    {
                        var ctrlType = ctrl.GetStringPropSafe("$type");
                        if (ctrlType.Contains("ControlGroupMetadata") || ctrlType.Contains("HeaderControlGroup") ||
                            ctrlType.Contains("FooterControlGroup") || ctrlType.Contains("ThreadControlGroup"))
                        {
                            var groupName = ctrl.GetStringPropSafe("Name");
                            var fields = new List<string>();

                            if (ctrl.TryGetProperty("Controls", out var innerControls) && innerControls.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var field in innerControls.EnumerateArray())
                                {
                                    var fieldName = field.GetStringPropSafe("Name");
                                    if (!string.IsNullOrEmpty(fieldName))
                                        fields.Add(fieldName);
                                }
                            }

                            var groupType = ctrlType.Contains("Header") ? "Header" :
                                            ctrlType.Contains("Footer") ? "Footer" :
                                            ctrlType.Contains("Thread") ? "Thread" : "Group";

                            controlGroups.Add(new ControlGroupPreview(groupName, groupType, fields));
                        }
                    }
                }
            }

            // Try to load resx labels
            var labels = new Dictionary<string, string>();
            var resxRuPath = Path.Combine(Path.GetDirectoryName(mtdPath)!, $"{entityName}System.ru.resx");
            if (File.Exists(resxRuPath))
            {
                try
                {
                    var xdoc = XDocument.Load(resxRuPath);
                    foreach (var data in xdoc.Descendants("data"))
                    {
                        var key = data.Attribute("name")?.Value ?? "";
                        var val = data.Element("value")?.Value ?? "";
                        if (!string.IsNullOrEmpty(key))
                            labels[key] = val;
                    }
                }
                catch { }
            }

            return new PreviewCardResult
            {
                Success = true,
                EntityName = entityName,
                BaseType = baseType,
                Properties = properties,
                ControlGroups = controlGroups,
                Labels = labels,
                DisplayName = labels.GetValueOrDefault("DisplayName", entityName),
                CollectionDisplayName = labels.GetValueOrDefault("CollectionDisplayName", entityName)
            };
        }
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        var path = parameters.TryGetValue("entityPath", out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? "" : "";
        return await PreviewAsync(path, ct);
    }

    private static PreviewCardResult Fail(string error) =>
        new() { Success = false, Errors = [error] };
}

public sealed record PreviewCardResult : ServiceResult
{
    public string EntityName { get; init; } = "";
    public string BaseType { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string CollectionDisplayName { get; init; } = "";
    public List<PreviewCardService.PropertyPreview> Properties { get; init; } = [];
    public List<PreviewCardService.ControlGroupPreview> ControlGroups { get; init; } = [];
    public Dictionary<string, string> Labels { get; init; } = new();

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Карточка: {DisplayName}");
        sb.AppendLine();
        sb.AppendLine($"**Сущность:** {EntityName}");
        sb.AppendLine($"**Тип:** {BaseType}");
        sb.AppendLine($"**Название (ед.):** {DisplayName}");
        sb.AppendLine($"**Название (мн.):** {CollectionDisplayName}");
        sb.AppendLine();

        // Properties table
        sb.AppendLine($"## Свойства ({Properties.Count})");
        sb.AppendLine();
        sb.AppendLine("| # | Имя | Тип | Подпись | Обяз. | Унасл. |");
        sb.AppendLine("|---|-----|-----|---------|-------|--------|");
        int idx = 1;
        foreach (var p in Properties)
        {
            var label = Labels.GetValueOrDefault($"Property_{p.Name}", "—");
            var req = p.IsRequired ? "да" : "";
            var anc = p.IsAncestor ? "да" : "";
            sb.AppendLine($"| {idx++} | {p.Name} | {p.Type} | {label} | {req} | {anc} |");
        }
        sb.AppendLine();

        // Card layout
        if (ControlGroups.Count > 0)
        {
            sb.AppendLine("## Раскладка карточки");
            sb.AppendLine();
            foreach (var cg in ControlGroups)
            {
                var icon = cg.GroupType switch
                {
                    "Header" => "[HEADER]",
                    "Footer" => "[FOOTER]",
                    "Thread" => "[THREAD]",
                    _ => "[GROUP]"
                };
                sb.AppendLine($"### {icon} {cg.Name}");
                if (cg.Fields.Count > 0)
                {
                    foreach (var f in cg.Fields)
                    {
                        var label = Labels.GetValueOrDefault($"Property_{f}", f);
                        sb.AppendLine($"  - {label} (`{f}`)");
                    }
                }
                else
                {
                    sb.AppendLine("  _(пусто)_");
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }
}

// Nested types for PreviewCardService
public partial class PreviewCardService
{
    public record PropertyPreview(string Name, string Type, string Guid, bool IsRequired, bool IsAncestor);
    public record ControlGroupPreview(string Name, string GroupType, List<string> Fields);
}
