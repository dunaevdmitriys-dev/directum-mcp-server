using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace DirectumMcp.Core.Validators;

/// <summary>
/// Severity level for validation issues.
/// </summary>
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}

/// <summary>
/// Result of a single validation check item.
/// </summary>
public record ValidationResult(
    ValidationSeverity Type,
    string CheckName,
    string Message,
    string? FilePath,
    bool CanAutoFix);

/// <summary>
/// Aggregated result of a single check (e.g. Check1) — used internally for report formatting.
/// </summary>
public record CheckResult(string Name, bool Passed, List<string> Issues, string Fix);

/// <summary>
/// Static validator for Directum RX .dat packages (unpacked directory).
/// Runs 7 checks against MTD and System.resx files.
/// </summary>
public static class PackageValidator
{
    // Well-known DatabookEntry base GUIDs in the Directum RX platform hierarchy.
    public static readonly HashSet<string> DatabookEntryGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        "04581d26-0780-4cfd-b3cd-c2cafc5798b0", // DatabookEntry
        "a2600e68-3e0a-4455-9f63-20bb01661658", // RecipientBase (inherits DatabookEntry)
    };

    public static readonly HashSet<string> CSharpReservedWords = new(StringComparer.Ordinal)
    {
        "new", "event", "class", "public", "private", "return", "void", "string",
        "int", "bool", "object", "base", "this", "null", "true", "false", "default",
        "switch", "case", "break", "continue", "for", "foreach", "while", "do",
        "if", "else", "try", "catch", "finally", "throw", "using", "namespace",
        "static", "abstract", "virtual", "override", "sealed", "readonly", "const",
        "delegate", "interface", "struct", "enum", "var", "is", "as", "in", "out",
        "ref", "params", "typeof", "sizeof", "checked", "unchecked", "fixed",
        "unsafe", "volatile", "extern", "lock", "goto", "implicit", "explicit",
        "operator", "stackalloc"
    };

    private static readonly Regex ResourceGuidKeyPattern = new(
        @"^Resource_[0-9a-fA-F]{8}[-]?[0-9a-fA-F]{4}[-]?[0-9a-fA-F]{4}[-]?[0-9a-fA-F]{4}[-]?[0-9a-fA-F]{12}$",
        RegexOptions.Compiled);

    /// <summary>
    /// Runs all 7 checks on an unpacked package directory.
    /// Returns a list of ValidationResult for each found issue.
    /// </summary>
    public static async Task<List<ValidationResult>> RunAllChecks(string workDir)
    {
        var mtdFiles = Directory.GetFiles(workDir, "*.mtd", SearchOption.AllDirectories);
        var resxFiles = Directory.GetFiles(workDir, "*System.resx", SearchOption.AllDirectories);

        var entities = new List<(string Path, JsonDocument Doc)>();
        var modules = new List<(string Path, JsonDocument Doc)>();

        foreach (var f in mtdFiles)
        {
            var json = await File.ReadAllTextAsync(f);
            var doc = JsonDocument.Parse(json);
            var typeProp = doc.RootElement.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
            if (typeProp.Contains("ModuleMetadata"))
                modules.Add((f, doc));
            else
                entities.Add((f, doc));
        }

        var results = new List<ValidationResult>();

        results.AddRange(Check1_CollectionOnDatabookEntry(entities));
        results.AddRange(Check2_CrossModuleNavProperties(entities, modules));
        results.AddRange(Check3_ReservedEnumNames(entities));
        results.AddRange(Check4_DuplicateCodes(entities));
        results.AddRange(Check5_AttachmentGroupConsistency(entities));
        results.AddRange(await Check6_ResxKeyFormat(resxFiles));
        results.AddRange(Check7_AnalyzersDirectory(workDir));

        // Dispose documents
        foreach (var (_, doc) in entities) doc.Dispose();
        foreach (var (_, doc) in modules) doc.Dispose();

        return results;
    }

    /// <summary>
    /// Runs all 7 checks and returns legacy CheckResult list for backward-compatible report formatting.
    /// </summary>
    public static async Task<(List<CheckResult> Results, int MtdCount, int ResxCount)> RunAllChecksLegacy(string workDir)
    {
        var mtdFiles = Directory.GetFiles(workDir, "*.mtd", SearchOption.AllDirectories);
        var resxFiles = Directory.GetFiles(workDir, "*System.resx", SearchOption.AllDirectories);

        var entities = new List<(string Path, JsonDocument Doc)>();
        var modules = new List<(string Path, JsonDocument Doc)>();

        foreach (var f in mtdFiles)
        {
            var json = await File.ReadAllTextAsync(f);
            var doc = JsonDocument.Parse(json);
            var typeProp = doc.RootElement.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
            if (typeProp.Contains("ModuleMetadata"))
                modules.Add((f, doc));
            else
                entities.Add((f, doc));
        }

        var results = new List<CheckResult>();

        results.Add(Check1_CollectionOnDatabookEntryLegacy(entities));
        results.Add(Check2_CrossModuleNavPropertiesLegacy(entities, modules));
        results.Add(Check3_ReservedEnumNamesLegacy(entities));
        results.Add(Check4_DuplicateCodesLegacy(entities));
        results.Add(Check5_AttachmentGroupConsistencyLegacy(entities));
        results.Add(await Check6_ResxKeyFormatLegacy(resxFiles));
        results.Add(Check7_AnalyzersDirectoryLegacy(workDir));

        // Dispose documents
        foreach (var (_, doc) in entities) doc.Dispose();
        foreach (var (_, doc) in modules) doc.Dispose();

        return (results, mtdFiles.Length, resxFiles.Length);
    }

    /// <summary>
    /// Runs all 7 checks using a pre-opened PackageWorkspace.
    /// Documents are NOT disposed — the workspace owns them.
    /// </summary>
    public static async Task<List<ValidationResult>> RunAllChecks(PackageWorkspace ws)
    {
        var results = new List<ValidationResult>();

        results.AddRange(Check1_CollectionOnDatabookEntry(ws.Entities));
        results.AddRange(Check2_CrossModuleNavProperties(ws.Entities, ws.Modules));
        results.AddRange(Check3_ReservedEnumNames(ws.Entities));
        results.AddRange(Check4_DuplicateCodes(ws.Entities));
        results.AddRange(Check5_AttachmentGroupConsistency(ws.Entities));
        results.AddRange(await Check6_ResxKeyFormat(ws.ResxFiles));
        results.AddRange(Check7_AnalyzersDirectory(ws.WorkDir));

        return results;
    }

    /// <summary>
    /// Runs all 7 checks using a pre-opened PackageWorkspace, returning legacy CheckResult format.
    /// Documents are NOT disposed — the workspace owns them.
    /// </summary>
    public static async Task<(List<CheckResult> Results, int MtdCount, int ResxCount)> RunAllChecksLegacy(PackageWorkspace ws)
    {
        var results = new List<CheckResult>();

        results.Add(Check1_CollectionOnDatabookEntryLegacy(ws.Entities));
        results.Add(Check2_CrossModuleNavPropertiesLegacy(ws.Entities, ws.Modules));
        results.Add(Check3_ReservedEnumNamesLegacy(ws.Entities));
        results.Add(Check4_DuplicateCodesLegacy(ws.Entities));
        results.Add(Check5_AttachmentGroupConsistencyLegacy(ws.Entities));
        results.Add(await Check6_ResxKeyFormatLegacy(ws.ResxFiles));
        results.Add(Check7_AnalyzersDirectoryLegacy(ws.WorkDir));

        return (results, ws.MtdFiles.Length, ws.ResxFiles.Length);
    }

    #region Check1: CollectionPropertyMetadata on DatabookEntry

    public static IEnumerable<ValidationResult> Check1_CollectionOnDatabookEntry(
        List<(string Path, JsonDocument Doc)> entities)
    {
        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            if (!HasCollectionProperties(root))
                continue;

            if (IsDatabookEntryDerived(root, entities))
            {
                var name = root.TryGetProperty("Name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(path);
                yield return new ValidationResult(
                    ValidationSeverity.Error,
                    "Check1_CollectionOnDatabookEntry",
                    $"`{name}` в `{Path.GetFileName(path)}` — DatabookEntry с CollectionPropertyMetadata",
                    path,
                    CanAutoFix: false);
            }
        }
    }

    private static CheckResult Check1_CollectionOnDatabookEntryLegacy(
        List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = new List<string>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            if (!HasCollectionProperties(root))
                continue;

            if (IsDatabookEntryDerived(root, entities))
            {
                var name = root.TryGetProperty("Name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(path);
                issues.Add($"  - `{name}` в `{Path.GetFileName(path)}` — DatabookEntry с CollectionPropertyMetadata");
            }
        }

        return new CheckResult(
            "CollectionPropertyMetadata в DatabookEntry",
            issues.Count == 0,
            issues,
            "Удалите CollectionPropertyMetadata или смените базовый тип сущности на Document."
        );
    }

    private static bool HasCollectionProperties(JsonElement root)
    {
        if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var prop in props.EnumerateArray())
        {
            if (prop.TryGetProperty("$type", out var t) &&
                (t.GetString()?.Contains("CollectionPropertyMetadata") ?? false))
                return true;
        }
        return false;
    }

    private static bool IsDatabookEntryDerived(JsonElement root, List<(string Path, JsonDocument Doc)> entities)
    {
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = root;

        while (true)
        {
            if (!current.TryGetProperty("BaseGuid", out var baseGuidEl))
                return false;

            var baseGuid = baseGuidEl.GetString();
            if (string.IsNullOrEmpty(baseGuid))
                return false;

            if (DatabookEntryGuids.Contains(baseGuid))
                return true;

            if (!visited.Add(baseGuid))
                return false; // circular reference guard

            var baseEntity = entities.FirstOrDefault(e =>
            {
                var r = e.Doc.RootElement;
                return r.TryGetProperty("NameGuid", out var ng) &&
                       string.Equals(ng.GetString(), baseGuid, StringComparison.OrdinalIgnoreCase);
            });

            if (baseEntity.Doc == null)
                return false;

            current = baseEntity.Doc.RootElement;
        }
    }

    #endregion

    #region Check2: Cross-module NavigationProperty references

    public static IEnumerable<ValidationResult> Check2_CrossModuleNavProperties(
        List<(string Path, JsonDocument Doc)> entities,
        List<(string Path, JsonDocument Doc)> modules)
    {
        var packageEntityGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, doc) in entities)
        {
            if (doc.RootElement.TryGetProperty("NameGuid", out var ng))
                packageEntityGuids.Add(ng.GetString() ?? "");
        }

        var dependencyModuleIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, doc) in modules)
        {
            if (doc.RootElement.TryGetProperty("Dependencies", out var deps) &&
                deps.ValueKind == JsonValueKind.Array)
            {
                foreach (var dep in deps.EnumerateArray())
                {
                    if (dep.TryGetProperty("Id", out var id))
                        dependencyModuleIds.Add(id.GetString() ?? "");
                }
            }
            if (doc.RootElement.TryGetProperty("NameGuid", out var moduleGuid))
                dependencyModuleIds.Add(moduleGuid.GetString() ?? "");
        }

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array)
                continue;

            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() : "?";

            foreach (var prop in props.EnumerateArray())
            {
                var typeName = prop.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                if (!typeName.Contains("NavigationPropertyMetadata"))
                    continue;

                if (!prop.TryGetProperty("EntityGuid", out var egEl))
                    continue;

                var entityGuid = egEl.GetString() ?? "";
                if (string.IsNullOrEmpty(entityGuid))
                    continue;

                if (packageEntityGuids.Contains(entityGuid))
                    continue;

                var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() : "?";
                yield return new ValidationResult(
                    ValidationSeverity.Warning,
                    "Check2_CrossModuleReferences",
                    $"`{entityName}.{propName}` -> EntityGuid `{entityGuid}` (внешняя ссылка — убедитесь, что модуль-источник указан в Dependencies)",
                    path,
                    CanAutoFix: false);
            }
        }
    }

    private static CheckResult Check2_CrossModuleNavPropertiesLegacy(
        List<(string Path, JsonDocument Doc)> entities,
        List<(string Path, JsonDocument Doc)> modules)
    {
        var issues = Check2_CrossModuleNavProperties(entities, modules)
            .Select(r => $"  - {r.Message}")
            .ToList();

        return new CheckResult(
            "Кросс-модульные ссылки NavigationProperty",
            issues.Count == 0,
            issues,
            "Добавьте модуль-владелец сущности в Dependencies в Module.mtd. Циклические зависимости запрещены."
        );
    }

    #endregion

    #region Check3: C# reserved words in enum values

    public static IEnumerable<ValidationResult> Check3_ReservedEnumNames(
        List<(string Path, JsonDocument Doc)> entities)
    {
        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() : "?";

            if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in props.EnumerateArray())
                {
                    var typeName = prop.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                    if (!typeName.Contains("EnumPropertyMetadata") && !typeName.Contains("EnumBlockPropertyMetadata"))
                        continue;

                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() : "?";
                    foreach (var result in CheckEnumValuesResults(prop, entityName!, propName!, path))
                        yield return result;
                }
            }

            if (root.TryGetProperty("Blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    if (block.TryGetProperty("OutProperties", out var outProps) && outProps.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var op in outProps.EnumerateArray())
                        {
                            var typeName = op.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                            if (!typeName.Contains("Enum"))
                                continue;
                            var opName = op.TryGetProperty("Name", out var pn) ? pn.GetString() : "?";
                            foreach (var result in CheckEnumValuesResults(op, entityName!, opName!, path))
                                yield return result;
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<ValidationResult> CheckEnumValuesResults(
        JsonElement prop, string entityName, string propName, string filePath)
    {
        if (!prop.TryGetProperty("DirectValues", out var vals) || vals.ValueKind != JsonValueKind.Array)
            yield break;

        foreach (var val in vals.EnumerateArray())
        {
            if (!val.TryGetProperty("Name", out var nameEl))
                continue;
            var valName = nameEl.GetString() ?? "";
            if (CSharpReservedWords.Contains(valName))
            {
                yield return new ValidationResult(
                    ValidationSeverity.Error,
                    "Check3_ReservedEnumValues",
                    $"`{entityName}.{propName}` значение `{valName}` — зарезервированное слово C#",
                    filePath,
                    CanAutoFix: true);
            }
        }
    }

    private static CheckResult Check3_ReservedEnumNamesLegacy(
        List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = Check3_ReservedEnumNames(entities)
            .Select(r => $"  - {r.Message}")
            .ToList();

        return new CheckResult(
            "Зарезервированные слова C# в значениях перечислений",
            issues.Count == 0,
            issues,
            "Переименуйте значение перечисления — Name не может быть зарезервированным словом C#."
        );
    }

    // Legacy helper for inline enum checking (used by ValidatePackageTool-compatible code)
    public static void CheckEnumValues(JsonElement prop, string entityName, string propName, List<string> issues)
    {
        if (!prop.TryGetProperty("DirectValues", out var vals) || vals.ValueKind != JsonValueKind.Array)
            return;

        foreach (var val in vals.EnumerateArray())
        {
            if (!val.TryGetProperty("Name", out var nameEl))
                continue;
            var valName = nameEl.GetString() ?? "";
            if (CSharpReservedWords.Contains(valName))
            {
                issues.Add($"  - `{entityName}.{propName}` значение `{valName}` — зарезервированное слово C#");
            }
        }
    }

    #endregion

    #region Check4: Duplicate DB column Codes

    public static IEnumerable<ValidationResult> Check4_DuplicateCodes(
        List<(string Path, JsonDocument Doc)> entities)
    {
        var codesByEntity = new Dictionary<string, List<(string EntityName, string PropName, string Code, string FilePath)>>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var baseGuid = root.TryGetProperty("BaseGuid", out var bg) ? bg.GetString() ?? "" : "";

            if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var prop in props.EnumerateArray())
            {
                if (!prop.TryGetProperty("Code", out var codeEl))
                    continue;
                var code = codeEl.GetString() ?? "";
                if (string.IsNullOrEmpty(code))
                    continue;

                var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                if (!string.IsNullOrEmpty(baseGuid))
                {
                    if (!codesByEntity.ContainsKey(baseGuid))
                        codesByEntity[baseGuid] = new List<(string, string, string, string)>();
                    codesByEntity[baseGuid].Add((entityName, propName, code, path));
                }
            }
        }

        foreach (var (baseGuid, entries) in codesByEntity)
        {
            var duplicates = entries
                .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && g.Select(x => x.EntityName).Distinct().Count() > 1);

            foreach (var group in duplicates)
            {
                var entitiesStr = string.Join(", ", group.Select(g => $"`{g.EntityName}.{g.PropName}`"));
                var firstPath = group.First().FilePath;
                yield return new ValidationResult(
                    ValidationSeverity.Error,
                    "Check4_DuplicateCodes",
                    $"Code `{group.Key}` дублируется в: {entitiesStr}",
                    firstPath,
                    CanAutoFix: true);
            }
        }
    }

    private static CheckResult Check4_DuplicateCodesLegacy(
        List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = Check4_DuplicateCodes(entities)
            .Select(r => $"  - {r.Message}")
            .ToList();

        return new CheckResult(
            "Дублирование Code свойств в иерархии наследования",
            issues.Count == 0,
            issues,
            "Дайте уникальные Code свойствам в одной иерархии наследования (например, CPDeal и InvDeal вместо двух Deal)."
        );
    }

    #endregion

    #region Check5: AttachmentGroup constraint consistency

    public static IEnumerable<ValidationResult> Check5_AttachmentGroupConsistency(
        List<(string Path, JsonDocument Doc)> entities)
    {
        var taskEntities = new List<(string Name, JsonElement Root)>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var typeProp = root.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
            var name = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";

            if (typeProp.Contains("TaskMetadata"))
                taskEntities.Add((name, root));
        }

        foreach (var (taskName, taskRoot) in taskEntities)
        {
            if (!taskRoot.TryGetProperty("AttachmentGroups", out var taskGroups) ||
                taskGroups.ValueKind != JsonValueKind.Array)
                continue;

            var taskConstraintsMap = new Dictionary<string, string>();
            foreach (var group in taskGroups.EnumerateArray())
            {
                var groupName = group.TryGetProperty("Name", out var gn) ? gn.GetString() ?? "" : "";
                var constraints = group.TryGetProperty("Constraints", out var c) ? c.GetRawText() : "[]";
                taskConstraintsMap[groupName] = constraints;
            }

            foreach (var (path, doc) in entities)
            {
                var root = doc.RootElement;
                var typeProp = root.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
                if (!typeProp.Contains("AssignmentMetadata") && !typeProp.Contains("NoticeMetadata"))
                    continue;

                if (!root.TryGetProperty("AttachmentGroups", out var assocGroups) ||
                    assocGroups.ValueKind != JsonValueKind.Array)
                    continue;

                var assocName = root.TryGetProperty("Name", out var an) ? an.GetString() ?? "?" : "?";

                foreach (var group in assocGroups.EnumerateArray())
                {
                    var isAssociated = group.TryGetProperty("IsAssociatedEntityGroup", out var iae) &&
                                      iae.GetBoolean();
                    if (!isAssociated)
                        continue;

                    var groupName = group.TryGetProperty("Name", out var gn) ? gn.GetString() ?? "" : "";
                    var constraints = group.TryGetProperty("Constraints", out var c) ? c.GetRawText() : "[]";

                    if (taskConstraintsMap.TryGetValue(groupName, out var taskConstraints) &&
                        taskConstraints != constraints)
                    {
                        yield return new ValidationResult(
                            ValidationSeverity.Error,
                            "Check5_AttachmentGroupConstraints",
                            $"Группа `{groupName}`: Task `{taskName}` и `{assocName}` имеют разные Constraints",
                            path,
                            CanAutoFix: true);
                    }
                }
            }
        }
    }

    private static CheckResult Check5_AttachmentGroupConsistencyLegacy(
        List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = Check5_AttachmentGroupConsistency(entities)
            .Select(r => $"  - {r.Message}")
            .ToList();

        return new CheckResult(
            "Согласованность AttachmentGroup Constraints (Task ↔ Assignment/Notice)",
            issues.Count == 0,
            issues,
            "Используйте одинаковые Constraints во всех связанных сущностях или пустые Constraints [] везде."
        );
    }

    #endregion

    #region Check6: System.resx key format

    public static async Task<IEnumerable<ValidationResult>> Check6_ResxKeyFormat(string[] resxFiles)
    {
        var results = new List<ValidationResult>();

        foreach (var file in resxFiles)
        {
            var xml = await File.ReadAllTextAsync(file);
            var xdoc = XDocument.Parse(xml);
            var dataElements = xdoc.Descendants("data");

            foreach (var data in dataElements)
            {
                var name = data.Attribute("name")?.Value ?? "";
                if (ResourceGuidKeyPattern.IsMatch(name))
                {
                    var value = data.Element("value")?.Value ?? "";
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        "Check6_ResxKeyFormat",
                        $"`{Path.GetFileName(file)}`: ключ `{name}` (значение: \"{value}\") — должен быть `Property_<Name>`",
                        file,
                        CanAutoFix: true));
                }
            }
        }

        return results;
    }

    private static async Task<CheckResult> Check6_ResxKeyFormatLegacy(string[] resxFiles)
    {
        var issues = (await Check6_ResxKeyFormat(resxFiles))
            .Select(r => $"  - {r.Message}")
            .ToList();

        return new CheckResult(
            "Формат ключей System.resx (Resource_<GUID> → Property_<Name>)",
            issues.Count == 0,
            issues,
            "Замените ключи Resource_<GUID> на Property_<PropertyName> в файлах *System.resx. " +
            "Runtime DDS 25.3 ищет подписи свойств только по ключу Property_<Name>."
        );
    }

    #endregion

    #region Check7: Analyzers directory

    public static IEnumerable<ValidationResult> Check7_AnalyzersDirectory(string workDir)
    {
        var sdsDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        var exists = Directory.Exists(sdsDir);
        var hasDlls = exists && Directory.GetFiles(sdsDir, "*.dll").Length > 0;

        if (!exists)
        {
            yield return new ValidationResult(
                ValidationSeverity.Warning,
                "Check7_AnalyzersDirectory",
                "Директория `.sds/Libraries/Analyzers/` не найдена",
                sdsDir,
                CanAutoFix: false);
        }
        else if (!hasDlls)
        {
            yield return new ValidationResult(
                ValidationSeverity.Warning,
                "Check7_AnalyzersDirectory",
                "Директория `.sds/Libraries/Analyzers/` существует, но не содержит DLL",
                sdsDir,
                CanAutoFix: false);
        }
    }

    private static CheckResult Check7_AnalyzersDirectoryLegacy(string workDir)
    {
        var issues = Check7_AnalyzersDirectory(workDir)
            .Select(r => $"  - {r.Message}")
            .ToList();

        return new CheckResult(
            "Наличие директории Analyzers",
            issues.Count == 0,
            issues,
            "Скопируйте содержимое <DDS_INSTALL>/Analyzers в .sds/Libraries/Analyzers/. " +
            "Примечание: эта проверка актуальна для git-репозитория решения, а не для .dat пакета."
        );
    }

    #endregion

    #region Helpers (public for tool reuse)

    /// <summary>
    /// Checks if a JSON element represents a DatabookEntry-derived entity.
    /// </summary>
    public static bool IsDatabookEntryDerivedPublic(JsonElement root, List<(string Path, JsonDocument Doc)> entities)
        => IsDatabookEntryDerived(root, entities);

    /// <summary>
    /// Checks if a JSON element has CollectionPropertyMetadata.
    /// </summary>
    public static bool HasCollectionPropertiesPublic(JsonElement root)
        => HasCollectionProperties(root);

    /// <summary>
    /// Regex to detect Resource_GUID keys in resx files.
    /// </summary>
    public static bool IsResourceGuidKey(string keyName)
        => ResourceGuidKeyPattern.IsMatch(keyName);

    /// <summary>
    /// Builds a map: entityName -> list of property names, from MTD files.
    /// Used for resolving resx Resource_GUID keys to Property_Name keys.
    /// </summary>
    public static async Task<Dictionary<string, List<string>>> BuildMtdPropertyMap(
        string[] mtdFiles, CancellationToken ct = default)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile, ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var typeProp = root.TryGetProperty("$type", out var tp) ? tp.GetString() ?? "" : "";
                if (typeProp.Contains("ModuleMetadata")) continue;

                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                if (string.IsNullOrEmpty(entityName)) continue;

                var propNames = new List<string>();
                if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prop in props.EnumerateArray())
                    {
                        if (prop.TryGetProperty("Name", out var pn))
                        {
                            var propName = pn.GetString();
                            if (!string.IsNullOrEmpty(propName))
                                propNames.Add(propName);
                        }
                    }
                }

                map[entityName] = propNames;
            }
            catch
            {
                // Skip unparseable files
            }
        }

        return map;
    }

    #endregion
}
