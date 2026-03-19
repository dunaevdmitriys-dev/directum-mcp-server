using System.ComponentModel;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Validators;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class FixPackageTool
{
    [McpServerTool(Name = "fix_package")]
    [Description("Автоисправление .dat: resx-ключи, дубли Code, enum, Constraints. dryRun по умолчанию.")]
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

        var (workspace, error) = await PackageWorkspace.OpenAsync(packagePath, includeRuResx: true, ct: cancellationToken);
        if (workspace == null)
            return error!;

        using (workspace)
        {
            // Detect manual-only issues using PackageValidator (single source of truth)
            var validationResults = await PackageValidator.RunAllChecks(workspace);

            var autoFixed = new List<ChangeRecord>();
            var manualRequired = new List<ManualIssue>();

            // Check 1 & 2 & 7: manual-only issues (detected by PackageValidator)
            foreach (var r in validationResults)
            {
                if (!r.CanAutoFix)
                {
                    manualRequired.Add(new ManualIssue(r.CheckName, Path.GetFileName(r.FilePath ?? ""), r.Message));
                }
            }

            // Check 3: Reserved enum names (auto-fixable) — always scan, fix logic has its own detection
            autoFixed.AddRange(await FixCheck3_ReservedEnumNames(workspace.Entities, dryRun, cancellationToken));

            // Check 4: Duplicate Codes (auto-fixable)
            autoFixed.AddRange(await FixCheck4_DuplicateCodes(workspace.Entities, dryRun, cancellationToken));

            // Check 5: AttachmentGroup Constraints (auto-fixable)
            autoFixed.AddRange(await FixCheck5_Constraints(workspace.Entities, dryRun, cancellationToken));

            // Check 6: System.resx key format (auto-fixable)
            autoFixed.AddRange(await FixCheck6_ResxKeys(workspace.ResxFiles, workspace.MtdFiles, dryRun, cancellationToken));

            // Repack if needed
            if (!dryRun && workspace.IsDatFile && autoFixed.Count > 0)
            {
                File.Delete(packagePath);
                ZipFile.CreateFromDirectory(workspace.WorkDir, packagePath);
            }

            return FormatFixReport(packagePath, autoFixed, manualRequired, dryRun);
        }
    }

    #region Fix Check3 — Reserved Enum Names

    private static async Task<List<ChangeRecord>> FixCheck3_ReservedEnumNames(
        List<(string Path, JsonDocument Doc)> entities,
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "?" : "?";
            var fileChanges = new List<(string PropName, string OldVal, string NewVal)>();

            CollectReservedEnumChanges(root, "Properties", entityName, fileChanges);
            CollectReservedEnumChangesFromBlocks(root, entityName, fileChanges);

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
                                if (name != null && PackageValidator.CSharpReservedWords.Contains(name))
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

    private static void CollectReservedEnumChanges(JsonElement root, string arrayPropName,
        string entityName, List<(string PropName, string OldVal, string NewVal)> fileChanges)
    {
        if (!root.TryGetProperty(arrayPropName, out var props) || props.ValueKind != JsonValueKind.Array)
            return;

        foreach (var prop in props.EnumerateArray())
        {
            var typeName = prop.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
            if (!typeName.Contains("EnumPropertyMetadata") && !typeName.Contains("EnumBlockPropertyMetadata"))
                continue;

            var propName = prop.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";
            CollectReservedValuesFromDirectValues(prop, propName, fileChanges);
        }
    }

    private static void CollectReservedEnumChangesFromBlocks(JsonElement root,
        string entityName, List<(string PropName, string OldVal, string NewVal)> fileChanges)
    {
        if (!root.TryGetProperty("Blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            return;

        foreach (var block in blocks.EnumerateArray())
        {
            if (!block.TryGetProperty("OutProperties", out var outProps) || outProps.ValueKind != JsonValueKind.Array)
                continue;

            foreach (var op in outProps.EnumerateArray())
            {
                var typeName = op.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                if (!typeName.Contains("Enum")) continue;
                var opName = op.TryGetProperty("Name", out var pn) ? pn.GetString() ?? "?" : "?";
                CollectReservedValuesFromDirectValues(op, opName, fileChanges);
            }
        }
    }

    private static void CollectReservedValuesFromDirectValues(JsonElement prop, string propName,
        List<(string PropName, string OldVal, string NewVal)> fileChanges)
    {
        if (!prop.TryGetProperty("DirectValues", out var vals) || vals.ValueKind != JsonValueKind.Array)
            return;

        foreach (var val in vals.EnumerateArray())
        {
            if (!val.TryGetProperty("Name", out var nameEl)) continue;
            var valName = nameEl.GetString() ?? "";
            if (PackageValidator.CSharpReservedWords.Contains(valName))
            {
                fileChanges.Add((propName, valName, valName + "Value"));
            }
        }
    }

    #endregion

    #region Fix Check4 — Duplicate Codes

    private static async Task<List<ChangeRecord>> FixCheck4_DuplicateCodes(
        List<(string Path, JsonDocument Doc)> entities,
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

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
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var typeProp = root.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";

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
        bool dryRun,
        CancellationToken ct)
    {
        var changes = new List<ChangeRecord>();

        var mtdPropertyMap = await PackageValidator.BuildMtdPropertyMap(mtdFiles, ct);

        foreach (var resxFile in resxFiles)
        {
            var xml = await File.ReadAllTextAsync(resxFile, ct);
            var xdoc = XDocument.Parse(xml);
            var dataElements = xdoc.Descendants("data").ToList();
            var modified = false;

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
                if (!PackageValidator.IsResourceGuidKey(keyName))
                    continue;

                var value = data.Element("value")?.Value ?? "";

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
