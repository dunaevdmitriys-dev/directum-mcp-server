using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class ConsistencyTools
{
    [McpServerTool(Name = "validate_guid_consistency")]
    [Description(
        "Cross-file GUID validation: Controls↔Properties, resx Form_GUID↔Forms, навигационные ссылки, Ribbon↔Actions, BaseGuid chain. " +
        "Используй после scaffold_entity или перед build_dat. " +
        "Для полной валидации вместе с другими проверками — validate_all.")]
    public async Task<string> ValidateGuidConsistency(
        [Description("Путь к директории модуля")] string modulePath)
    {
        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория не найдена: `{modulePath}`";

        var sb = new StringBuilder();
        sb.AppendLine("# GUID Consistency Report");
        sb.AppendLine();
        sb.AppendLine($"**Модуль**: `{modulePath}`");
        sb.AppendLine();

        var mtdFiles = Directory.GetFiles(modulePath, "*.mtd", SearchOption.AllDirectories);
        var resxFiles = Directory.GetFiles(modulePath, "*System.resx", SearchOption.AllDirectories);

        int totalErrors = 0, totalWarnings = 0, parseErrors = 0;
        var entities = new List<(string Path, string Name, JsonDocument Doc)>();
        JsonDocument? moduleDoc = null;

        foreach (var f in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(f);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var typeProp = root.GetStringProp("$type");
                var name = root.GetStringProp("Name");

                if (typeProp.Contains("ModuleMetadata"))
                    moduleDoc = doc;
                else
                    entities.Add((f, name, doc));
            }
            catch { parseErrors++; }
        }

        // Check 1: Controls ↔ Properties
        sb.AppendLine("## 1. Controls ↔ Properties");
        var (c1e, c1w) = CheckControlsProperties(sb, entities);
        totalErrors += c1e; totalWarnings += c1w;
        if (c1e == 0 && c1w == 0) sb.AppendLine("PASS");
        sb.AppendLine();

        // Check 2: resx GUID ↔ MTD
        sb.AppendLine("## 2. Resx GUID ↔ MTD");
        var (c2e, c2w) = await CheckResxGuids(sb, entities, resxFiles);
        totalErrors += c2e; totalWarnings += c2w;
        if (c2e == 0 && c2w == 0) sb.AppendLine("PASS");
        sb.AppendLine();

        // Check 3: Navigation EntityGuid
        sb.AppendLine("## 3. NavigationProperty EntityGuid");
        var (c3e, c3w) = CheckNavigationProperties(sb, entities, moduleDoc);
        totalErrors += c3e; totalWarnings += c3w;
        if (c3e == 0 && c3w == 0) sb.AppendLine("PASS");
        sb.AppendLine();

        // Check 4: Ribbon ↔ Actions
        sb.AppendLine("## 4. Ribbon ActionGuid ↔ Actions");
        var (c4e, c4w) = CheckRibbonActions(sb, entities);
        totalErrors += c4e; totalWarnings += c4w;
        if (c4e == 0 && c4w == 0) sb.AppendLine("PASS");
        sb.AppendLine();

        // Check 5: BaseGuid chain
        sb.AppendLine("## 5. BaseGuid наследования");
        var (c5e, c5w) = CheckBaseGuidChain(sb, entities);
        totalErrors += c5e; totalWarnings += c5w;
        if (c5e == 0 && c5w == 0) sb.AppendLine("PASS");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine($"**MTD**: {mtdFiles.Length} | **Сущности**: {entities.Count} | **Errors**: {totalErrors} | **Warnings**: {totalWarnings}");
        sb.AppendLine($"**Вердикт**: {(totalErrors == 0 ? "PASS" : "FAIL")}");

        foreach (var (_, _, doc) in entities) doc.Dispose();
        moduleDoc?.Dispose();

        return sb.ToString();
    }

    #region Check Methods (copied from DevTools — same logic)

    private static (int errors, int warnings) CheckControlsProperties(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities)
    {
        int errors = 0, warnings = 0;
        foreach (var (_, entityName, doc) in entities)
        {
            var root = doc.RootElement;
            var propertyGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                foreach (var prop in props.EnumerateArray())
                    if (prop.TryGetProperty("NameGuid", out var ng))
                        propertyGuids.Add(ng.GetString() ?? "");

            var controlGroupGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!root.TryGetProperty("Forms", out var forms) || forms.ValueKind != JsonValueKind.Array) continue;

            foreach (var form in forms.EnumerateArray())
            {
                if (!form.TryGetProperty("Controls", out var controls) || controls.ValueKind != JsonValueKind.Array) continue;
                foreach (var ctrl in controls.EnumerateArray())
                {
                    if (ctrl.GetStringProp("$type").Contains("ControlGroupMetadata") && ctrl.TryGetProperty("NameGuid", out var cgGuid))
                        controlGroupGuids.Add(cgGuid.GetString() ?? "");
                }
                foreach (var ctrl in controls.EnumerateArray())
                {
                    if (!ctrl.GetStringProp("$type").Contains("ControlMetadata") || ctrl.GetStringProp("$type").Contains("ControlGroupMetadata")) continue;
                    if (ctrl.TryGetProperty("IsAncestorMetadata", out var isAnc) && isAnc.ValueKind == JsonValueKind.True) continue;
                    var ctrlName = ctrl.GetStringProp("Name");
                    if (ctrl.TryGetProperty("PropertyGuid", out var pgEl))
                    {
                        var pg = pgEl.GetString() ?? "";
                        if (!string.IsNullOrEmpty(pg) && !propertyGuids.Contains(pg))
                        { sb.AppendLine($"- ERROR `{entityName}.{ctrlName}`: PropertyGuid `{pg}` не найден"); errors++; }
                    }
                    if (ctrl.TryGetProperty("ParentGuid", out var parentEl))
                    {
                        var parent = parentEl.GetString() ?? "";
                        if (!string.IsNullOrEmpty(parent) && !controlGroupGuids.Contains(parent))
                        { sb.AppendLine($"- WARN `{entityName}.{ctrlName}`: ParentGuid `{parent}` не найден"); warnings++; }
                    }
                }
            }
        }
        return (errors, warnings);
    }

    private static async Task<(int errors, int warnings)> CheckResxGuids(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities, string[] resxFiles)
    {
        int errors = 0, warnings = 0;
        var knownGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, doc) in entities) CollectGuidsRecursive(doc.RootElement, knownGuids);

        foreach (var resxFile in resxFiles)
        {
            try
            {
                var xdoc = XDocument.Parse(await File.ReadAllTextAsync(resxFile));
                foreach (var data in xdoc.Descendants("data"))
                {
                    var key = data.Attribute("name")?.Value ?? "";
                    var guid = ExtractGuidFromResxKey(key);
                    if (guid != null && !knownGuids.Contains(guid))
                    { sb.AppendLine($"- WARN `{Path.GetFileName(resxFile)}`: `{key}` → GUID `{guid}` не найден в MTD"); warnings++; }
                }
            }
            catch { }
        }
        return (errors, warnings);
    }

    private static (int errors, int warnings) CheckNavigationProperties(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities, JsonDocument? moduleDoc)
    {
        int errors = 0, warnings = 0;
        var localGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, doc) in entities)
            if (doc.RootElement.TryGetProperty("NameGuid", out var ng))
                localGuids.Add(ng.GetString() ?? "");

        foreach (var (_, entityName, doc) in entities)
        {
            if (!doc.RootElement.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array) continue;
            foreach (var prop in props.EnumerateArray())
            {
                if (!prop.GetStringProp("$type").Contains("NavigationPropertyMetadata")) continue;
                if (prop.TryGetProperty("IsAncestorMetadata", out var isAnc) && isAnc.ValueKind == JsonValueKind.True) continue;
                var entityGuid = prop.GetStringProp("EntityGuid");
                if (string.IsNullOrEmpty(entityGuid) || localGuids.Contains(entityGuid) || DirectumConstants.KnownBaseGuids.ContainsKey(entityGuid)) continue;
                sb.AppendLine($"- WARN `{entityName}.{prop.GetStringProp("Name")}`: EntityGuid `{entityGuid}` — внешняя ссылка");
                warnings++;
            }
        }
        return (errors, warnings);
    }

    private static (int errors, int warnings) CheckRibbonActions(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities)
    {
        int errors = 0, warnings = 0;
        foreach (var (_, entityName, doc) in entities)
        {
            var root = doc.RootElement;
            var actionGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
                foreach (var a in actions.EnumerateArray())
                    if (a.TryGetProperty("NameGuid", out var ng)) actionGuids.Add(ng.GetString() ?? "");
            if (actionGuids.Count == 0) continue;

            foreach (var ribbonProp in new[] { "RibbonCardMetadata", "RibbonCollectionMetadata" })
            {
                if (!root.TryGetProperty(ribbonProp, out var ribbon)) continue;
                if (!ribbon.TryGetProperty("Groups", out var groups) || groups.ValueKind != JsonValueKind.Array) continue;
                foreach (var group in groups.EnumerateArray())
                {
                    if (!group.TryGetProperty("Elements", out var elements) || elements.ValueKind != JsonValueKind.Array) continue;
                    foreach (var el in elements.EnumerateArray())
                    {
                        if (el.TryGetProperty("IsAncestorMetadata", out var isAnc) && isAnc.ValueKind == JsonValueKind.True) continue;
                        var ag = el.GetStringProp("ActionGuid");
                        if (!string.IsNullOrEmpty(ag) && !actionGuids.Contains(ag))
                        { sb.AppendLine($"- ERROR `{entityName}` Ribbon: ActionGuid `{ag}` не найден"); errors++; }
                    }
                }
            }
        }
        return (errors, warnings);
    }

    private static (int errors, int warnings) CheckBaseGuidChain(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities)
    {
        int errors = 0, warnings = 0;
        var guidToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, name, doc) in entities)
        {
            var guid = doc.RootElement.GetStringProp("NameGuid");
            if (!string.IsNullOrEmpty(guid)) guidToName[guid] = name;
        }
        foreach (var (_, entityName, doc) in entities)
        {
            var baseGuid = doc.RootElement.GetStringProp("BaseGuid");
            if (string.IsNullOrEmpty(baseGuid) || DirectumConstants.KnownBaseGuids.ContainsKey(baseGuid) || guidToName.ContainsKey(baseGuid)) continue;
            sb.AppendLine($"- WARN `{entityName}`: BaseGuid `{baseGuid}` не найден локально");
            warnings++;
        }
        return (errors, warnings);
    }

    private static void CollectGuidsRecursive(JsonElement element, HashSet<string> guids)
    {
        if (element.ValueKind == JsonValueKind.Object)
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.EndsWith("Guid") && prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(val) && val.Contains('-') && val.Length >= 32) guids.Add(val);
                }
                else CollectGuidsRecursive(prop.Value, guids);
            }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CollectGuidsRecursive(item, guids);
    }

    private static string? ExtractGuidFromResxKey(string key)
    {
        var parts = key.Split('_');
        if (parts.Length < 2) return null;
        var last = parts[^1];
        return last.Length >= 32 && last.All(c => "0123456789abcdefABCDEF-".Contains(c)) ? last : null;
    }

    #endregion
}
