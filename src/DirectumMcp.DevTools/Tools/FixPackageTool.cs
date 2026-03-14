using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class FixPackageTool
{
    private static readonly HashSet<string> DatabookEntryGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        "04581d26-0780-4cfd-b3cd-c2cafc5798b0", // DatabookEntry
        "a2600e68-3e0a-4455-9f63-20bb01661658", // RecipientBase
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

    [McpServerTool(Name = "fix_package")]
    [Description("Автоисправление ошибок .dat пакета Directum RX (resx-ключи, дубли Code, enum, Constraints). dryRun=true по умолчанию.")]
    public async Task<string> Fix(
        [Description("Путь к .dat файлу или директории с распакованным пакетом")]
        string packagePath,

        [Description("Если true (по умолчанию) - показывает план исправлений без изменения файлов. " +
                     "Если false - применяет исправления и перепаковывает .dat")]
        bool dryRun = true,

        CancellationToken cancellationToken = default)
    {
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

        string workDir;
        bool isTempDir = false;
        bool isDatFile = false;

        if (File.Exists(packagePath) && packagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            workDir = Path.Combine(Path.GetTempPath(), "drx_fix_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(workDir);
            isTempDir = true;
            isDatFile = true;

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
            var resxFiles = Directory.GetFiles(workDir, "*System.resx", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(workDir, "*System.ru.resx", SearchOption.AllDirectories))
                .Distinct()
                .ToArray();

            // Parse all MTD files into raw JSON for validation
            var entities = new List<(string Path, JsonDocument Doc)>();
            var modules = new List<(string Path, JsonDocument Doc)>();

            foreach (var f in mtdFiles)
            {
                var json = await File.ReadAllTextAsync(f, cancellationToken);
                var doc = JsonDocument.Parse(json);
                var typeProp = doc.RootElement.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                if (typeProp.Contains("ModuleMetadata"))
                    modules.Add((f, doc));
                else
                    entities.Add((f, doc));
            }

            var autoFixed = new List<ChangeRecord>();
            var manualRequired = new List<ManualIssue>();

            // Check 1: CollectionProperty on DatabookEntry (manual only)
            Check1_CollectionOnDatabookEntry(entities, manualRequired);

            // Check 2: Cross-module NavigationProperty (manual only)
            Check2_CrossModuleNavProperties(entities, modules, manualRequired);

            // Check 3: Reserved enum names (auto-fixable)
            var check3Changes = await FixCheck3_ReservedEnumNames(entities, workDir, dryRun, cancellationToken);
            autoFixed.AddRange(check3Changes);

            // Check 4: Duplicate Codes (auto-fixable)
            var check4Changes = await FixCheck4_DuplicateCodes(entities, workDir, dryRun, cancellationToken);
            autoFixed.AddRange(check4Changes);

            // Check 5: AttachmentGroup Constraints (auto-fixable)
            var check5Changes = await FixCheck5_Constraints(entities, workDir, dryRun, cancellationToken);
            autoFixed.AddRange(check5Changes);

            // Check 6: System.resx key format (auto-fixable)
            var check6Changes = await FixCheck6_ResxKeys(resxFiles, mtdFiles, workDir, dryRun, cancellationToken);
            autoFixed.AddRange(check6Changes);

            // Check 7: Analyzers directory (manual only)
            Check7_AnalyzersDirectory(workDir, manualRequired);

            // Dispose documents
            foreach (var (_, doc) in entities) doc.Dispose();
            foreach (var (_, doc) in modules) doc.Dispose();

            // Repack if needed
            if (!dryRun && isDatFile && autoFixed.Count > 0)
            {
                File.Delete(packagePath);
                ZipFile.CreateFromDirectory(workDir, packagePath);
            }

            return FormatFixReport(packagePath, autoFixed, manualRequired, dryRun);
        }
        finally
        {
            if (isTempDir)
            {
                try { Directory.Delete(workDir, true); } catch { /* best effort */ }
            }
        }
    }

    #region Check1 & Check2 & Check7 — Manual Only

    private static void Check1_CollectionOnDatabookEntry(
        List<(string Path, JsonDocument Doc)> entities,
        List<ManualIssue> issues)
    {
        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            if (!HasCollectionProperties(root))
                continue;

            if (IsDatabookEntryDerived(root, entities))
            {
                var name = root.TryGetProperty("Name", out var n) ? n.GetString() : Path.GetFileNameWithoutExtension(path);
                issues.Add(new ManualIssue("Check1",
                    Path.GetFileName(path),
                    $"DatabookEntry `{name}` содержит CollectionPropertyMetadata. " +
                    "Удалите коллекции или смените базовый тип на Document."));
            }
        }
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
            if (string.IsNullOrEmpty(baseGuid)) return false;
            if (DatabookEntryGuids.Contains(baseGuid)) return true;
            if (!visited.Add(baseGuid)) return false;

            var baseEntity = entities.FirstOrDefault(e =>
            {
                var r = e.Doc.RootElement;
                return r.TryGetProperty("NameGuid", out var ng) &&
                       string.Equals(ng.GetString(), baseGuid, StringComparison.OrdinalIgnoreCase);
            });

            if (baseEntity.Doc == null) return false;
            current = baseEntity.Doc.RootElement;
        }
    }

    private static void Check2_CrossModuleNavProperties(
        List<(string Path, JsonDocument Doc)> entities,
        List<(string Path, JsonDocument Doc)> modules,
        List<ManualIssue> issues)
    {
        var packageEntityGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (_, doc) in entities)
        {
            if (doc.RootElement.TryGetProperty("NameGuid", out var ng))
                packageEntityGuids.Add(ng.GetString() ?? "");
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
                if (!typeName.Contains("NavigationPropertyMetadata")) continue;
                if (!prop.TryGetProperty("EntityGuid", out var egEl)) continue;

                var entityGuid = egEl.GetString() ?? "";
                if (string.IsNullOrEmpty(entityGuid) || packageEntityGuids.Contains(entityGuid))
                    continue;

                var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() : "?";
                issues.Add(new ManualIssue("Check2",
                    Path.GetFileName(path),
                    $"`{entityName}.{propName}` -> EntityGuid `{entityGuid}` (внешняя ссылка). " +
                    "Убедитесь, что модуль-источник указан в Dependencies."));
            }
        }
    }

    private static void Check7_AnalyzersDirectory(string workDir, List<ManualIssue> issues)
    {
        var sdsDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        var exists = Directory.Exists(sdsDir);
        var hasDlls = exists && Directory.GetFiles(sdsDir, "*.dll").Length > 0;

        if (!exists)
            issues.Add(new ManualIssue("Check7", ".sds/Libraries/Analyzers/",
                "Директория Analyzers не найдена. Скопируйте из <DDS_INSTALL>/Analyzers."));
        else if (!hasDlls)
            issues.Add(new ManualIssue("Check7", ".sds/Libraries/Analyzers/",
                "Директория Analyzers существует, но не содержит DLL."));
    }

    #endregion

    #region Fix Check3 — Reserved Enum Names

    private static async Task<List<ChangeRecord>> FixCheck3_ReservedEnumNames(
        List<(string Path, JsonDocument Doc)> entities,
        string workDir,
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var fileChanges = new List<(string PropName, string OldVal, string NewVal)>();

            if (root.TryGetProperty("Properties", out var props) && props.ValueKind == JsonValueKind.Array)
            {
                foreach (var prop in props.EnumerateArray())
                {
                    var typeName = prop.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                    if (!typeName.Contains("EnumPropertyMetadata") && !typeName.Contains("EnumBlockPropertyMetadata"))
                        continue;

                    var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                    if (prop.TryGetProperty("DirectValues", out var vals) && vals.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var val in vals.EnumerateArray())
                        {
                            if (!val.TryGetProperty("Name", out var nameEl)) continue;
                            var valName = nameEl.GetString() ?? "";
                            if (CSharpReservedWords.Contains(valName))
                            {
                                fileChanges.Add((propName, valName, valName + "Value"));
                            }
                        }
                    }
                }
            }

            // Also check Blocks OutProperties
            if (root.TryGetProperty("Blocks", out var blocks) && blocks.ValueKind == JsonValueKind.Array)
            {
                foreach (var block in blocks.EnumerateArray())
                {
                    if (block.TryGetProperty("OutProperties", out var outProps) && outProps.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var op in outProps.EnumerateArray())
                        {
                            var typeName = op.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                            if (!typeName.Contains("Enum")) continue;
                            var opName = op.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                            if (op.TryGetProperty("DirectValues", out var vals) && vals.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var val in vals.EnumerateArray())
                                {
                                    if (!val.TryGetProperty("Name", out var nameEl)) continue;
                                    var valName = nameEl.GetString() ?? "";
                                    if (CSharpReservedWords.Contains(valName))
                                    {
                                        fileChanges.Add((opName, valName, valName + "Value"));
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (fileChanges.Count > 0)
            {
                if (!dryRun)
                {
                    var jsonText = await File.ReadAllTextAsync(path, ct);
                    var jsonNode = JsonNode.Parse(jsonText);
                    var properties = jsonNode?["Properties"]?.AsArray();
                    if (properties != null)
                    {
                        foreach (var prop in properties)
                        {
                            var directValues = prop?["DirectValues"]?.AsArray();
                            if (directValues == null) continue;
                            foreach (var dv in directValues)
                            {
                                var name = dv?["Name"]?.GetValue<string>();
                                if (name != null && CSharpReservedWords.Contains(name))
                                {
                                    dv!["Name"] = name + "Value";
                                }
                            }
                        }
                    }
                    jsonText = jsonNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(path, jsonText, ct);
                }

                foreach (var (propName, oldVal, newVal) in fileChanges)
                {
                    changes.Add(new ChangeRecord("Check3",
                        Path.GetFileName(path),
                        $"{entityName}.{propName}.DirectValues[].Name = \"{oldVal}\"",
                        $"\"{newVal}\""));
                }
            }
        }

        return changes;
    }

    #endregion

    #region Fix Check4 — Duplicate Codes

    private static async Task<List<ChangeRecord>> FixCheck4_DuplicateCodes(
        List<(string Path, JsonDocument Doc)> entities,
        string workDir,
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        // Group properties by BaseGuid to find siblings
        var codesByBase = new Dictionary<string, List<(string EntityName, string PropName, string Code, string FilePath)>>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var baseGuid = root.TryGetProperty("BaseGuid", out var bg) ? bg.GetString() ?? "" : "";

            if (string.IsNullOrEmpty(baseGuid)) continue;
            if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var prop in props.EnumerateArray())
            {
                if (!prop.TryGetProperty("Code", out var codeEl)) continue;
                var code = codeEl.GetString() ?? "";
                if (string.IsNullOrEmpty(code)) continue;

                var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";

                if (!codesByBase.ContainsKey(baseGuid))
                    codesByBase[baseGuid] = [];
                codesByBase[baseGuid].Add((entityName, propName, code, path));
            }
        }

        // Find duplicates and generate fixes
        var fileFixes = new Dictionary<string, List<(string OldCode, string NewCode)>>();

        foreach (var (_, entries) in codesByBase)
        {
            var duplicates = entries
                .GroupBy(e => e.Code, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1 && g.Select(x => x.EntityName).Distinct().Count() > 1);

            foreach (var group in duplicates)
            {
                var usedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in group)
                {
                    // Generate prefix from entity name (first 2 uppercase letters)
                    var prefix = new string(item.EntityName
                        .Where(char.IsUpper)
                        .Take(2)
                        .ToArray());
                    if (prefix.Length < 2)
                        prefix = item.EntityName.Length >= 2
                            ? item.EntityName[..2]
                            : item.EntityName;

                    var newCode = prefix + item.Code;
                    var suffix = 2;
                    while (usedCodes.Contains(newCode))
                    {
                        newCode = prefix + item.Code + "_" + suffix;
                        suffix++;
                    }
                    usedCodes.Add(newCode);

                    if (!fileFixes.ContainsKey(item.FilePath))
                        fileFixes[item.FilePath] = [];
                    fileFixes[item.FilePath].Add((item.Code, newCode));

                    changes.Add(new ChangeRecord("Check4",
                        Path.GetFileName(item.FilePath),
                        $"{item.EntityName}.{item.PropName}.Code = \"{item.Code}\"",
                        $"\"{newCode}\""));
                }
            }
        }

        if (!dryRun && fileFixes.Count > 0)
        {
            foreach (var (filePath, fixes) in fileFixes)
            {
                var jsonText = await File.ReadAllTextAsync(filePath, ct);
                var jsonNode = JsonNode.Parse(jsonText);
                var properties = jsonNode?["Properties"]?.AsArray();
                if (properties != null)
                {
                    foreach (var prop in properties)
                    {
                        var code = prop?["Code"]?.GetValue<string>();
                        var matchingFix = fixes.FirstOrDefault(f => f.OldCode == code);
                        if (matchingFix.OldCode != null)
                        {
                            prop!["Code"] = matchingFix.NewCode;
                        }
                    }
                }
                jsonText = jsonNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, jsonText, ct);
            }
        }

        return changes;
    }

    #endregion

    #region Fix Check5 — Constraints

    private static async Task<List<ChangeRecord>> FixCheck5_Constraints(
        List<(string Path, JsonDocument Doc)> entities,
        string workDir,
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var typeProp = root.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";

            // Only fix Assignment and Notice entities
            if (!typeProp.Contains("AssignmentMetadata") && !typeProp.Contains("NoticeMetadata"))
                continue;

            if (!root.TryGetProperty("AttachmentGroups", out var groups) || groups.ValueKind != JsonValueKind.Array)
                continue;

            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var hasNonEmptyConstraints = false;

            foreach (var group in groups.EnumerateArray())
            {
                var isAssociated = group.TryGetProperty("IsAssociatedEntityGroup", out var iae) && iae.GetBoolean();
                if (!isAssociated) continue;

                if (group.TryGetProperty("Constraints", out var constraints) &&
                    constraints.ValueKind == JsonValueKind.Array &&
                    constraints.GetArrayLength() > 0)
                {
                    var groupName = group.TryGetProperty("Name", out var gn) ? gn.GetString() ?? "?" : "?";
                    var constraintCount = constraints.GetArrayLength();
                    hasNonEmptyConstraints = true;
                    changes.Add(new ChangeRecord("Check5",
                        Path.GetFileName(path),
                        $"{entityName}.AttachmentGroups[\"{groupName}\"].Constraints ({constraintCount} элементов)",
                        "[]"));
                }
            }

            if (hasNonEmptyConstraints && !dryRun)
            {
                var jsonText = await File.ReadAllTextAsync(path, ct);
                // Parse as JsonNode for modification
                var jsonNode = JsonNode.Parse(jsonText);
                if (jsonNode?["AttachmentGroups"] is JsonArray groupsArray)
                {
                    foreach (var groupNode in groupsArray)
                    {
                        if (groupNode is not JsonObject groupObj) continue;
                        var isAssoc = groupObj["IsAssociatedEntityGroup"]?.GetValue<bool>() ?? false;
                        if (!isAssoc) continue;

                        if (groupObj["Constraints"] is JsonArray constraintsArr && constraintsArr.Count > 0)
                        {
                            groupObj["Constraints"] = new JsonArray();
                        }
                    }
                }
                var options = new JsonSerializerOptions { WriteIndented = true };
                await File.WriteAllTextAsync(path, jsonNode!.ToJsonString(options), ct);
            }
        }

        return changes;
    }

    #endregion

    #region Fix Check6 — Resx Keys

    private static async Task<List<ChangeRecord>> FixCheck6_ResxKeys(
        string[] resxFiles,
        string[] mtdFiles,
        string workDir,
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        // Build property map from MTD files
        var mtdPropertyMap = await BuildMtdPropertyMap(mtdFiles, ct);

        foreach (var resxFile in resxFiles)
        {
            var xml = await File.ReadAllTextAsync(resxFile, ct);
            var xdoc = XDocument.Parse(xml);
            var dataElements = xdoc.Descendants("data").ToList();
            var modified = false;

            // Determine entity name from resx file name
            var fileName = Path.GetFileNameWithoutExtension(resxFile);
            if (fileName.EndsWith(".ru", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^3];
            if (fileName.EndsWith("System", StringComparison.OrdinalIgnoreCase))
                fileName = fileName[..^6];

            List<string>? propNames = null;
            if (!string.IsNullOrEmpty(fileName))
                mtdPropertyMap.TryGetValue(fileName, out propNames);

            foreach (var data in dataElements)
            {
                var keyName = data.Attribute("name")?.Value ?? "";
                if (!ResourceGuidKeyPattern.IsMatch(keyName))
                    continue;

                var value = data.Element("value")?.Value ?? "";

                // Try to find matching property name
                string? newKey = null;
                if (propNames != null)
                {
                    var match = propNames.FirstOrDefault(p =>
                        string.Equals(p, value, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                        newKey = $"Property_{match}";
                }

                if (newKey != null)
                {
                    changes.Add(new ChangeRecord("Check6",
                        Path.GetFileName(resxFile),
                        $"ключ `{keyName}` (значение: \"{value}\")",
                        $"`{newKey}`"));

                    if (!dryRun)
                    {
                        data.SetAttributeValue("name", newKey);
                        modified = true;
                    }
                }
                else
                {
                    // Could not auto-resolve — still record as a change with suggestion
                    changes.Add(new ChangeRecord("Check6",
                        Path.GetFileName(resxFile),
                        $"ключ `{keyName}` (значение: \"{value}\")",
                        "_(не удалось определить автоматически, требуется ручное исправление)_"));
                }
            }

            if (modified && !dryRun)
            {
                xdoc.Save(resxFile);
            }
        }

        return changes;
    }

    private static async Task<Dictionary<string, List<string>>> BuildMtdPropertyMap(
        string[] mtdFiles, CancellationToken ct)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile, ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Skip module MTD
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

    #region Report Formatting

    private static string FormatFixReport(
        string packagePath,
        List<ChangeRecord> autoFixed,
        List<ManualIssue> manualRequired,
        bool dryRun)
    {
        var sb = new StringBuilder();
        var packageName = Path.GetFileName(packagePath);

        sb.AppendLine($"# Результат исправления пакета {packageName}");
        sb.AppendLine();

        // Auto-fixed section
        var autoFixableChanges = autoFixed.Where(c =>
            !c.After.Contains("не удалось определить автоматически")).ToList();
        var unresolvableChanges = autoFixed.Where(c =>
            c.After.Contains("не удалось определить автоматически")).ToList();

        sb.AppendLine($"## Исправлено автоматически ({autoFixableChanges.Count})");
        sb.AppendLine();

        if (autoFixableChanges.Count > 0)
        {
            sb.AppendLine("| # | Проверка | Файл | Было | Стало |");
            sb.AppendLine("|---|---------|------|------|-------|");

            int idx = 1;
            foreach (var change in autoFixableChanges)
            {
                sb.AppendLine($"| {idx++} | {change.CheckId} | `{change.FileName}` | {change.Before} | {change.After} |");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("_(нет проблем для автоматического исправления)_");
            sb.AppendLine();
        }

        // Manual required section
        var allManual = manualRequired
            .Select(m => (m.CheckId, m.FileName, m.Description))
            .Concat(unresolvableChanges.Select(c => (c.CheckId, c.FileName, Description: $"{c.Before} -> {c.After}")))
            .ToList();

        sb.AppendLine($"## Требует ручного исправления ({allManual.Count})");
        sb.AppendLine();

        if (allManual.Count > 0)
        {
            sb.AppendLine("| # | Проверка | Файл | Описание |");
            sb.AppendLine("|---|---------|------|----------|");

            int idx = 1;
            foreach (var issue in allManual)
            {
                sb.AppendLine($"| {idx++} | {issue.CheckId} | `{issue.FileName}` | {issue.Description} |");
            }
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("_(нет проблем, требующих ручного исправления)_");
            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("## Итого");
        sb.AppendLine($"- Исправлено автоматически: {autoFixableChanges.Count}");
        sb.AppendLine($"- Требует ручного исправления: {allManual.Count}");
        sb.AppendLine($"- Режим: {(dryRun ? "предпросмотр (dryRun=true)" : "изменения применены")}");
        sb.AppendLine();

        if (dryRun && autoFixableChanges.Count > 0)
            sb.AppendLine("Режим предпросмотра. Запустите с `dryRun=false` для применения исправлений.");

        return sb.ToString();
    }

    #endregion

    private record ChangeRecord(string CheckId, string FileName, string Before, string After);
    private record ManualIssue(string CheckId, string FileName, string Description);
}
