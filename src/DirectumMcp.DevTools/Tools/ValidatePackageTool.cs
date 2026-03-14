using System.ComponentModel;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidatePackageTool
{
    // Well-known DatabookEntry base GUIDs in the Directum RX platform hierarchy.
    // DatabookEntry itself and its common ancestors.
    private static readonly HashSet<string> DatabookEntryGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        "04581d26-0780-4cfd-b3cd-c2cafc5798b0", // DatabookEntry
        "a2600e68-3e0a-4455-9f63-20bb01661658", // RecipientBase (inherits DatabookEntry)
    };

    private static readonly HashSet<string> CSharpReservedWords = new(StringComparer.Ordinal)
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

    [McpServerTool(Name = "check_package")]
    [Description("Валидация .dat пакета Directum RX перед импортом в DDS. " +
                 "Проверяет 7 типичных проблем: CollectionProperty в DatabookEntry, " +
                 "кросс-модульные ссылки, зарезервированные слова C#, дублирование Code, " +
                 "согласованность AttachmentGroup, формат ключей System.resx, наличие Analyzers.")]
    public async Task<string> Validate(string packagePath)
    {
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

        string workDir;
        bool isTempDir = false;

        if (File.Exists(packagePath) && packagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            workDir = Path.Combine(Path.GetTempPath(), "drx_validate_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(workDir);
            isTempDir = true;

            // Safe extraction to prevent Zip Slip
            using var archive = ZipFile.OpenRead(packagePath);
            foreach (var entry in archive.Entries)
            {
                var destPath = Path.GetFullPath(Path.Combine(workDir, entry.FullName));
                if (!destPath.StartsWith(Path.GetFullPath(workDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"Zip entry '{entry.FullName}' would extract outside target directory.");

                var dir = Path.GetDirectoryName(destPath);
                if (dir != null) Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(entry.Name))
                    entry.ExtractToFile(destPath, overwrite: true);
            }
        }
        else if (Directory.Exists(packagePath))
        {
            workDir = packagePath;
        }
        else
        {
            return $"**ОШИБКА**: Путь не найден: `{packagePath}`\nУкажите путь к .dat файлу или распакованной директории.";
        }

        try
        {
            var mtdFiles = Directory.GetFiles(workDir, "*.mtd", SearchOption.AllDirectories);
            var resxFiles = Directory.GetFiles(workDir, "*System.resx", SearchOption.AllDirectories);

            // Parse all MTD files
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

            // Check 1: CollectionPropertyMetadata on DatabookEntry
            results.Add(Check1_CollectionOnDatabookEntry(entities));

            // Check 2: Cross-module NavigationProperty references
            results.Add(Check2_CrossModuleNavProperties(entities, modules));

            // Check 3: C# reserved words in enum values
            results.Add(Check3_ReservedEnumNames(entities));

            // Check 4: Duplicate DB column Codes
            results.Add(Check4_DuplicateCodes(entities));

            // Check 5: AttachmentGroup constraint consistency
            results.Add(Check5_AttachmentGroupConsistency(entities));

            // Check 6: System.resx key format
            results.Add(await Check6_ResxKeyFormat(resxFiles));

            // Check 7: Analyzers directory
            results.Add(Check7_AnalyzersDirectory(workDir));

            // Dispose documents
            foreach (var (_, doc) in entities) doc.Dispose();
            foreach (var (_, doc) in modules) doc.Dispose();

            return FormatReport(packagePath, mtdFiles.Length, resxFiles.Length, results);
        }
        finally
        {
            if (isTempDir)
            {
                try { Directory.Delete(workDir, true); } catch { /* best effort */ }
            }
        }
    }

    private CheckResult Check1_CollectionOnDatabookEntry(List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = new List<string>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            if (!HasCollectionProperties(root))
                continue;

            // Check if entity or any ancestor is DatabookEntry
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

            // Try to find the base entity among parsed files
            var baseEntity = entities.FirstOrDefault(e =>
            {
                var r = e.Doc.RootElement;
                return r.TryGetProperty("NameGuid", out var ng) &&
                       string.Equals(ng.GetString(), baseGuid, StringComparison.OrdinalIgnoreCase);
            });

            if (baseEntity.Doc == null)
                return false; // base not found in package — can't determine, skip

            current = baseEntity.Doc.RootElement;
        }
    }

    private CheckResult Check2_CrossModuleNavProperties(
        List<(string Path, JsonDocument Doc)> entities,
        List<(string Path, JsonDocument Doc)> modules)
    {
        var issues = new List<string>();

        // Collect all entity GUIDs defined in this package
        var packageEntityGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, doc) in entities)
        {
            if (doc.RootElement.TryGetProperty("NameGuid", out var ng))
                packageEntityGuids.Add(ng.GetString() ?? "");
        }

        // Collect dependency module IDs from all Module.mtd
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
            // Also add the module's own NameGuid
            if (doc.RootElement.TryGetProperty("NameGuid", out var moduleGuid))
                dependencyModuleIds.Add(moduleGuid.GetString() ?? "");
        }

        // Check NavigationPropertyMetadata references
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

                // Skip if the referenced entity is in this package
                if (packageEntityGuids.Contains(entityGuid))
                    continue;

                // For external references — we can't fully resolve without the platform catalog,
                // so we flag a warning if we detect the reference is to an unknown entity
                var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() : "?";
                issues.Add($"  - `{entityName}.{propName}` -> EntityGuid `{entityGuid}` (внешняя ссылка — убедитесь, что модуль-источник указан в Dependencies)");
            }
        }

        return new CheckResult(
            "Кросс-модульные ссылки NavigationProperty",
            issues.Count == 0,
            issues,
            "Добавьте модуль-владелец сущности в Dependencies в Module.mtd. Циклические зависимости запрещены."
        );
    }

    private CheckResult Check3_ReservedEnumNames(List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = new List<string>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() : "?";

            // Check Properties for EnumPropertyMetadata with DirectValues
            if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in props.EnumerateArray())
                {
                    var typeName = prop.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                    if (!typeName.Contains("EnumPropertyMetadata") && !typeName.Contains("EnumBlockPropertyMetadata"))
                        continue;

                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() : "?";
                    CheckEnumValues(prop, entityName!, propName!, issues);
                }
            }

            // Check Blocks for enum OutProperties
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
                            CheckEnumValues(op, entityName!, opName!, issues);
                        }
                    }
                }
            }
        }

        return new CheckResult(
            "Зарезервированные слова C# в значениях перечислений",
            issues.Count == 0,
            issues,
            "Переименуйте значение перечисления — Name не может быть зарезервированным словом C#."
        );
    }

    private static void CheckEnumValues(JsonElement prop, string entityName, string propName, List<string> issues)
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

    private CheckResult Check4_DuplicateCodes(List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = new List<string>();

        // Group entities by BaseGuid to detect inheritance hierarchies
        // Then check for duplicate Code values within properties
        var codesByEntity = new Dictionary<string, List<(string EntityName, string PropName, string Code)>>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var entityGuid = root.TryGetProperty("NameGuid", out var ng) ? ng.GetString() ?? "" : "";
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

                // Use baseGuid as hierarchy key — entities with same base are siblings
                if (!string.IsNullOrEmpty(baseGuid))
                {
                    if (!codesByEntity.ContainsKey(baseGuid))
                        codesByEntity[baseGuid] = new List<(string, string, string)>();
                    codesByEntity[baseGuid].Add((entityName, propName, code));
                }
            }
        }

        // Find duplicates within each hierarchy
        foreach (var (baseGuid, entries) in codesByEntity)
        {
            var duplicates = entries
                .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && g.Select(x => x.EntityName).Distinct().Count() > 1);

            foreach (var group in duplicates)
            {
                var entitiesStr = string.Join(", ", group.Select(g => $"`{g.EntityName}.{g.PropName}`"));
                issues.Add($"  - Code `{group.Key}` дублируется в: {entitiesStr}");
            }
        }

        return new CheckResult(
            "Дублирование Code свойств в иерархии наследования",
            issues.Count == 0,
            issues,
            "Дайте уникальные Code свойствам в одной иерархии наследования (например, CPDeal и InvDeal вместо двух Deal)."
        );
    }

    private CheckResult Check5_AttachmentGroupConsistency(List<(string Path, JsonDocument Doc)> entities)
    {
        var issues = new List<string>();

        // Find Task entities and their associated Assignment/Notice entities
        var taskEntities = new List<(string Name, JsonElement Root)>();
        var allEntities = new Dictionary<string, (string Name, JsonElement Root)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var typeProp = root.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
            var name = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var guid = root.TryGetProperty("NameGuid", out var ng) ? ng.GetString() ?? "" : "";

            if (!string.IsNullOrEmpty(guid))
                allEntities[guid] = (name, root);

            if (typeProp.Contains("TaskMetadata"))
                taskEntities.Add((name, root));
        }

        foreach (var (taskName, taskRoot) in taskEntities)
        {
            if (!taskRoot.TryGetProperty("AttachmentGroups", out var taskGroups) ||
                taskGroups.ValueKind != JsonValueKind.Array)
                continue;

            // Serialize task constraints for comparison
            var taskConstraintsMap = new Dictionary<string, string>();
            foreach (var group in taskGroups.EnumerateArray())
            {
                var groupName = group.TryGetProperty("Name", out var gn) ? gn.GetString() ?? "" : "";
                var constraints = group.TryGetProperty("Constraints", out var c) ? c.GetRawText() : "[]";
                taskConstraintsMap[groupName] = constraints;
            }

            // Check associated Assignment/Notice entities that share AttachmentGroups
            foreach (var (_, doc) in entities)
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
                        issues.Add($"  - Группа `{groupName}`: Task `{taskName}` и `{assocName}` имеют разные Constraints");
                    }
                }
            }
        }

        return new CheckResult(
            "Согласованность AttachmentGroup Constraints (Task ↔ Assignment/Notice)",
            issues.Count == 0,
            issues,
            "Используйте одинаковые Constraints во всех связанных сущностях или пустые Constraints [] везде."
        );
    }

    private async Task<CheckResult> Check6_ResxKeyFormat(string[] resxFiles)
    {
        var issues = new List<string>();

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
                    issues.Add($"  - `{Path.GetFileName(file)}`: ключ `{name}` (значение: \"{value}\") — должен быть `Property_<Name>`");
                }
            }
        }

        return new CheckResult(
            "Формат ключей System.resx (Resource_<GUID> → Property_<Name>)",
            issues.Count == 0,
            issues,
            "Замените ключи Resource_<GUID> на Property_<PropertyName> в файлах *System.resx. " +
            "Runtime DDS 25.3 ищет подписи свойств только по ключу Property_<Name>."
        );
    }

    private CheckResult Check7_AnalyzersDirectory(string workDir)
    {
        var sdsDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        var exists = Directory.Exists(sdsDir);
        var hasDlls = exists && Directory.GetFiles(sdsDir, "*.dll").Length > 0;

        var issues = new List<string>();
        if (!exists)
            issues.Add("  - Директория `.sds/Libraries/Analyzers/` не найдена");
        else if (!hasDlls)
            issues.Add("  - Директория `.sds/Libraries/Analyzers/` существует, но не содержит DLL");

        return new CheckResult(
            "Наличие директории Analyzers",
            issues.Count == 0,
            issues,
            "Скопируйте содержимое <DDS_INSTALL>/Analyzers в .sds/Libraries/Analyzers/. " +
            "Примечание: эта проверка актуальна для git-репозитория решения, а не для .dat пакета."
        );
    }

    private static string FormatReport(string packagePath, int mtdCount, int resxCount, List<CheckResult> results)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# Результат валидации пакета");
        sb.AppendLine();
        sb.AppendLine($"**Пакет**: `{packagePath}`");
        sb.AppendLine($"**MTD файлов**: {mtdCount}");
        sb.AppendLine($"**System.resx файлов**: {resxCount}");
        sb.AppendLine();

        int passed = results.Count(r => r.Passed);
        int failed = results.Count(r => !r.Passed);
        sb.AppendLine($"**Итого**: {passed} проверок пройдено, {failed} проблем найдено");
        sb.AppendLine();

        foreach (var (i, result) in results.Select((r, i) => (i + 1, r)))
        {
            var status = result.Passed ? "PASS" : "FAIL";
            sb.AppendLine($"## {i}. [{status}] {result.Name}");

            if (result.Issues.Count > 0)
            {
                sb.AppendLine();
                foreach (var issue in result.Issues)
                    sb.AppendLine(issue);
                sb.AppendLine();
                sb.AppendLine($"**Рекомендация**: {result.Fix}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private record CheckResult(string Name, bool Passed, List<string> Issues, string Fix);
}
