using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateGuidConsistencyTool
{
    [McpServerTool(Name = "validate_guid_consistency")]
    [Description("Cross-file GUID validation: Controls↔Properties, resx Form_GUID↔Forms, навигационные ссылки.")]
    public async Task<string> Execute(
        [Description("Путь к директории модуля (например source/DirRX.CRMSales)")] string modulePath)
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория не найдена: `{modulePath}`";

        var sb = new StringBuilder();
        sb.AppendLine("# GUID Consistency Report");
        sb.AppendLine();
        sb.AppendLine($"**Модуль**: `{modulePath}`");
        sb.AppendLine();

        var mtdFiles = Directory.GetFiles(modulePath, "*.mtd", SearchOption.AllDirectories);
        var resxFiles = Directory.GetFiles(modulePath, "*System.resx", SearchOption.AllDirectories);

        int totalErrors = 0;
        int totalWarnings = 0;
        int totalInfos = 0;
        int parseErrors = 0;

        var entities = new List<(string Path, string Name, JsonDocument Doc)>();
        JsonDocument? moduleDoc = null;
        string? moduleDocPath = null;

        // Parse all MTD files
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
                {
                    moduleDoc = doc;
                    moduleDocPath = f;
                }
                else
                {
                    entities.Add((f, name, doc));
                }
            }
            catch
            {
                parseErrors++;
            }
        }

        // Check 1: Controls ↔ Properties GUID consistency
        sb.AppendLine("## 1. Controls ↔ Properties");
        sb.AppendLine();
        var (c1errors, c1warnings, c1infos) = CheckControlsProperties(sb, entities);
        totalErrors += c1errors;
        totalWarnings += c1warnings;
        totalInfos += c1infos;
        if (c1errors == 0 && c1warnings == 0 && c1infos == 0)
            sb.AppendLine("PASS — все контролы ссылаются на существующие свойства");
        sb.AppendLine();

        // Check 2: resx Form_GUID ↔ Forms in MTD
        sb.AppendLine("## 2. Resx GUID ↔ MTD Forms/ControlGroups");
        sb.AppendLine();
        var (c2errors, c2warnings) = await CheckResxGuids(sb, entities, resxFiles);
        totalErrors += c2errors;
        totalWarnings += c2warnings;
        if (c2errors == 0 && c2warnings == 0)
            sb.AppendLine("PASS — все GUID в resx ссылаются на существующие элементы MTD");
        sb.AppendLine();

        // Check 3: NavigationProperty EntityGuid references
        sb.AppendLine("## 3. NavigationProperty EntityGuid");
        sb.AppendLine();
        var (c3errors, c3warnings, c3infos) = CheckNavigationProperties(sb, entities, moduleDoc);
        totalErrors += c3errors;
        totalWarnings += c3warnings;
        totalInfos += c3infos;
        if (c3errors == 0 && c3warnings == 0 && c3infos == 0)
            sb.AppendLine("PASS — все навигационные свойства ссылаются на существующие сущности");
        sb.AppendLine();

        // Check 4: Action GUID consistency (RibbonElements → Actions)
        sb.AppendLine("## 4. Ribbon ActionGuid ↔ Actions");
        sb.AppendLine();
        var (c4errors, c4warnings) = CheckRibbonActions(sb, entities);
        totalErrors += c4errors;
        totalWarnings += c4warnings;
        if (c4errors == 0 && c4warnings == 0)
            sb.AppendLine("PASS — все ActionGuid в Ribbon ссылаются на существующие действия");
        sb.AppendLine();

        // Check 5: Cross-entity BaseGuid references
        sb.AppendLine("## 5. BaseGuid наследования");
        sb.AppendLine();
        var (c5errors, c5warnings) = CheckBaseGuidChain(sb, entities);
        totalErrors += c5errors;
        totalWarnings += c5warnings;
        if (c5errors == 0 && c5warnings == 0)
            sb.AppendLine("PASS — цепочки наследования корректны");
        sb.AppendLine();

        // Summary
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Итого");
        sb.AppendLine();
        sb.AppendLine($"- **MTD файлов**: {mtdFiles.Length}");
        sb.AppendLine($"- **Resx файлов**: {resxFiles.Length}");
        sb.AppendLine($"- **Сущностей**: {entities.Count}");
        sb.AppendLine($"- **Ошибок (ERROR)**: {totalErrors}");
        sb.AppendLine($"- **Предупреждений (WARN)**: {totalWarnings}");
        sb.AppendLine($"- **Информационных (INFO)**: {totalInfos}");
        if (parseErrors > 0)
            sb.AppendLine($"- **Ошибок парсинга**: {parseErrors}");

        var verdict = totalErrors == 0 ? "PASS" : "FAIL";
        sb.AppendLine();
        sb.AppendLine($"**Вердикт**: {verdict}");

        // Cleanup
        foreach (var (_, _, doc) in entities) doc.Dispose();
        moduleDoc?.Dispose();

        return sb.ToString();
    }

    /// <summary>
    /// Build a map of NameGuid → JsonDocument for all local entities (for BaseGuid chain resolution).
    /// </summary>
    private static Dictionary<string, JsonDocument> BuildGuidToDocMap(
        List<(string Path, string Name, JsonDocument Doc)> entities)
    {
        var map = new Dictionary<string, JsonDocument>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, doc) in entities)
        {
            var guid = doc.RootElement.GetStringProp("NameGuid");
            if (!string.IsNullOrEmpty(guid))
                map[guid] = doc;
        }
        return map;
    }

    /// <summary>
    /// Collect property GUIDs from a single entity's Properties array.
    /// </summary>
    private static HashSet<string> CollectPropertyGuids(JsonElement root)
    {
        var guids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            foreach (var prop in props.EnumerateArray())
                if (prop.TryGetProperty("NameGuid", out var ng))
                    guids.Add(ng.GetString() ?? "");
        return guids;
    }

    /// <summary>
    /// Walk the BaseGuid inheritance chain and collect all inherited property GUIDs.
    /// Returns true if the full chain was resolved (all ancestors found locally or no base).
    /// Returns false if an ancestor was not found (platform type or external dependency).
    /// </summary>
    private static bool CollectInheritedPropertyGuids(
        JsonElement root,
        Dictionary<string, JsonDocument> guidToDoc,
        HashSet<string> result)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = root;

        while (true)
        {
            var baseGuid = current.GetStringProp("BaseGuid");
            if (string.IsNullOrEmpty(baseGuid))
                break;

            // Prevent infinite loops
            if (!visited.Add(baseGuid))
                break;

            // Platform base type — we don't have its properties locally, but controls
            // referencing platform properties are valid (inherited from Assignment, Task, etc.)
            if (DirectumConstants.KnownBaseGuids.ContainsKey(baseGuid))
                return false;

            // Local entity in the same package
            if (guidToDoc.TryGetValue(baseGuid, out var baseDoc))
            {
                var baseRoot = baseDoc.RootElement;
                var baseProps = CollectPropertyGuids(baseRoot);
                foreach (var pg in baseProps)
                    result.Add(pg);
                current = baseRoot;
                continue;
            }

            // Base entity not found locally and not a known platform type — dependency module
            return false;
        }

        return true;
    }

    private static (int errors, int warnings, int infos) CheckControlsProperties(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities)
    {
        int errors = 0, warnings = 0, infos = 0;
        var guidToDoc = BuildGuidToDocMap(entities);

        foreach (var (path, entityName, doc) in entities)
        {
            var root = doc.RootElement;

            // Collect local property GUIDs
            var propertyGuids = CollectPropertyGuids(root);

            // Collect inherited property GUIDs via BaseGuid chain
            var inheritedGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var chainFullyResolved = CollectInheritedPropertyGuids(root, guidToDoc, inheritedGuids);

            // Merge inherited into the full set
            var allPropertyGuids = new HashSet<string>(propertyGuids, StringComparer.OrdinalIgnoreCase);
            foreach (var ig in inheritedGuids)
                allPropertyGuids.Add(ig);

            // Collect ControlGroup GUIDs
            var controlGroupGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!root.TryGetProperty("Forms", out var forms) || forms.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var form in forms.EnumerateArray())
            {
                if (!form.TryGetProperty("Controls", out var controls) || controls.ValueKind != JsonValueKind.Array)
                    continue;

                // First pass: collect ControlGroup GUIDs
                foreach (var ctrl in controls.EnumerateArray())
                {
                    var ctrlType = ctrl.GetStringProp("$type");
                    if (ctrlType.Contains("ControlGroupMetadata"))
                    {
                        if (ctrl.TryGetProperty("NameGuid", out var cgGuid))
                            controlGroupGuids.Add(cgGuid.GetString() ?? "");
                    }
                }

                // Second pass: validate
                foreach (var ctrl in controls.EnumerateArray())
                {
                    var ctrlType = ctrl.GetStringProp("$type");
                    if (!ctrlType.Contains("ControlMetadata") || ctrlType.Contains("ControlGroupMetadata"))
                        continue;

                    if (ctrl.TryGetProperty("IsAncestorMetadata", out var isAnc) && isAnc.ValueKind == JsonValueKind.True)
                        continue;

                    var ctrlName = ctrl.GetStringProp("Name");

                    if (ctrl.TryGetProperty("PropertyGuid", out var pgEl))
                    {
                        var pg = pgEl.GetString() ?? "";
                        if (!string.IsNullOrEmpty(pg) && !allPropertyGuids.Contains(pg))
                        {
                            if (!chainFullyResolved)
                            {
                                // Chain ends at platform/external base — property likely inherited from ancestor
                                sb.AppendLine($"- **INFO** `{entityName}.{ctrlName}`: PropertyGuid `{pg}` не найден локально (вероятно, унаследован от платформенного предка)");
                                infos++;
                            }
                            else
                            {
                                sb.AppendLine($"- **ERROR** `{entityName}.{ctrlName}`: PropertyGuid `{pg}` не найден в Properties");
                                errors++;
                            }
                        }
                    }

                    if (ctrl.TryGetProperty("ParentGuid", out var parentEl))
                    {
                        var parent = parentEl.GetString() ?? "";
                        if (!string.IsNullOrEmpty(parent) && !controlGroupGuids.Contains(parent))
                        {
                            sb.AppendLine($"- **WARN** `{entityName}.{ctrlName}`: ParentGuid `{parent}` не найден в ControlGroups");
                            warnings++;
                        }
                    }
                }
            }
        }

        return (errors, warnings, infos);
    }

    private static async Task<(int errors, int warnings)> CheckResxGuids(
        StringBuilder sb,
        List<(string Path, string Name, JsonDocument Doc)> entities,
        string[] resxFiles)
    {
        int errors = 0, warnings = 0;

        // Collect all known GUIDs from MTD files
        var knownGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (_, _, doc) in entities)
        {
            var root = doc.RootElement;
            CollectGuidsRecursive(root, knownGuids);
        }

        // Check resx files for GUID references
        foreach (var resxFile in resxFiles)
        {
            try
            {
                var xml = await File.ReadAllTextAsync(resxFile);
                var xdoc = XDocument.Parse(xml);
                var fileName = Path.GetFileName(resxFile);

                foreach (var data in xdoc.Descendants("data"))
                {
                    var key = data.Attribute("name")?.Value ?? "";

                    // Check Form_GUID, ControlGroup_GUID, Ribbon_*_GUID patterns
                    var guid = ExtractGuidFromResxKey(key);
                    if (guid != null && !knownGuids.Contains(guid))
                    {
                        var value = data.Element("value")?.Value ?? "";
                        sb.AppendLine($"- **WARN** `{fileName}`: ключ `{key}` (значение: \"{value}\") ссылается на GUID `{guid}`, не найденный в MTD");
                        warnings++;
                    }
                }
            }
            catch
            {
                // Skip unparseable resx
            }
        }

        return (errors, warnings);
    }

    private static (int errors, int warnings, int infos) CheckNavigationProperties(
        StringBuilder sb,
        List<(string Path, string Name, JsonDocument Doc)> entities,
        JsonDocument? moduleDoc)
    {
        int errors = 0, warnings = 0, infos = 0;

        // Collect all entity GUIDs in the module
        var localEntityGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, _, doc) in entities)
        {
            if (doc.RootElement.TryGetProperty("NameGuid", out var ng))
                localEntityGuids.Add(ng.GetString() ?? "");
        }

        // Collect dependency module IDs
        var dependencyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (moduleDoc != null)
        {
            var root = moduleDoc.RootElement;
            if (root.TryGetProperty("Dependencies", out var deps) && deps.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    if (dep.TryGetProperty("Id", out var id))
                        dependencyIds.Add(id.GetString() ?? "");
                }
            }
        }

        // Well-known platform entity GUIDs (Sungero base types)
        var platformGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "04581d26-0780-4cfd-b3cd-c2cafc5798b0", // DatabookEntry
            "58cca102-1e97-4f07-b6ac-fd866a8b7cb1", // Document
            "d795d1f6-45c1-4e5e-9677-b53fb7280c7e", // Task
            "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24", // Assignment
            "4e09273f-8b3a-489e-814e-a4ebfbba3e6c", // Notice
            "b7905516-2be5-4931-961c-cb38d5677565", // Person
            "294767f1-009f-4fde-9571-c18a938de4c5", // Company
            "f5509cdc-ac0c-4507-a4d3-61d7a0a9b6cf", // Contact
            "c612fc41-44a3-428b-a97c-433c333d78e9", // Employee
            "eff95720-181d-4571-a73f-02f1f089ca13", // Department
            "78278dd3-5e3d-4be5-81ea-a858c6c6d820", // BusinessUnit
            "a2600e68-3e0a-4455-9f63-20bb01661658", // RecipientBase
        };

        foreach (var (path, entityName, doc) in entities)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var prop in props.EnumerateArray())
            {
                var typeName = prop.GetStringProp("$type");
                if (!typeName.Contains("NavigationPropertyMetadata"))
                    continue;

                if (prop.TryGetProperty("IsAncestorMetadata", out var isAnc) && isAnc.ValueKind == JsonValueKind.True)
                    continue;

                var entityGuid = prop.GetStringProp("EntityGuid");
                if (string.IsNullOrEmpty(entityGuid))
                    continue;

                var propName = prop.GetStringProp("Name");

                if (localEntityGuids.Contains(entityGuid) || platformGuids.Contains(entityGuid))
                    continue;

                // External reference — INFO, not ERROR/WARN (likely from dependency module)
                sb.AppendLine($"- **INFO** `{entityName}.{propName}`: EntityGuid `{entityGuid}` — внешняя ссылка (зависимый модуль)");
                infos++;
            }
        }

        return (errors, warnings, infos);
    }

    private static (int errors, int warnings) CheckRibbonActions(
        StringBuilder sb, List<(string Path, string Name, JsonDocument Doc)> entities)
    {
        int errors = 0, warnings = 0;

        foreach (var (path, entityName, doc) in entities)
        {
            var root = doc.RootElement;

            // Collect action GUIDs
            var actionGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("Actions", out var actions) && actions.ValueKind == JsonValueKind.Array)
            {
                foreach (var action in actions.EnumerateArray())
                {
                    if (action.TryGetProperty("NameGuid", out var ng))
                        actionGuids.Add(ng.GetString() ?? "");
                }
            }

            if (actionGuids.Count == 0)
                continue;

            // Check Ribbon references
            foreach (var ribbonProp in new[] { "RibbonCardMetadata", "RibbonCollectionMetadata" })
            {
                if (!root.TryGetProperty(ribbonProp, out var ribbon))
                    continue;

                if (!ribbon.TryGetProperty("Groups", out var groups) || groups.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var group in groups.EnumerateArray())
                {
                    if (!group.TryGetProperty("Elements", out var elements) || elements.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var element in elements.EnumerateArray())
                    {
                        if (element.TryGetProperty("IsAncestorMetadata", out var isAnc) && isAnc.ValueKind == JsonValueKind.True)
                            continue;

                        var actionGuid = element.GetStringProp("ActionGuid");
                        if (string.IsNullOrEmpty(actionGuid))
                            continue;

                        var elemName = element.GetStringProp("Name");

                        if (!actionGuids.Contains(actionGuid))
                        {
                            sb.AppendLine($"- **ERROR** `{entityName}` Ribbon `{elemName}`: ActionGuid `{actionGuid}` не найден в Actions");
                            errors++;
                        }
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

        var guidToDoc = BuildGuidToDocMap(entities);
        var guidToName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, name, doc) in entities)
        {
            var guid = doc.RootElement.GetStringProp("NameGuid");
            if (!string.IsNullOrEmpty(guid))
                guidToName[guid] = name;
        }

        foreach (var (_, entityName, doc) in entities)
        {
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var current = doc.RootElement;

            while (true)
            {
                var baseGuid = current.GetStringProp("BaseGuid");
                if (string.IsNullOrEmpty(baseGuid))
                    break;

                // Prevent infinite loops
                if (!visited.Add(baseGuid))
                {
                    sb.AppendLine($"- **ERROR** `{entityName}`: Циклическая ссылка BaseGuid `{baseGuid}`");
                    errors++;
                    break;
                }

                // Known platform base type — chain is valid
                if (DirectumConstants.KnownBaseGuids.ContainsKey(baseGuid))
                    break;

                // Local entity — continue walking the chain
                if (guidToDoc.TryGetValue(baseGuid, out var baseDoc))
                {
                    current = baseDoc.RootElement;
                    continue;
                }

                // Unknown base — might be from a dependency module
                sb.AppendLine($"- **WARN** `{entityName}`: BaseGuid `{baseGuid}` не найден локально (возможно, из зависимого модуля)");
                warnings++;
                break;
            }
        }

        return (errors, warnings);
    }

    private static void CollectGuidsRecursive(JsonElement element, HashSet<string> guids)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                if (prop.Name.EndsWith("Guid", StringComparison.Ordinal) &&
                    prop.Value.ValueKind == JsonValueKind.String)
                {
                    var val = prop.Value.GetString();
                    if (!string.IsNullOrEmpty(val) && val.Contains('-') && val.Length >= 32)
                        guids.Add(val);
                }
                else
                {
                    CollectGuidsRecursive(prop.Value, guids);
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
                CollectGuidsRecursive(item, guids);
        }
    }

    private static string? ExtractGuidFromResxKey(string key)
    {
        // Patterns: Form_GUID, ControlGroup_GUID, Ribbon_Name_GUID, FilterPanel_Name_GUID
        var parts = key.Split('_');
        if (parts.Length < 2) return null;

        var lastPart = parts[^1];
        // Check if last part is a GUID (32+ hex chars)
        if (lastPart.Length >= 32 && lastPart.All(c => "0123456789abcdefABCDEF-".Contains(c)))
            return lastPart;

        return null;
    }
}
