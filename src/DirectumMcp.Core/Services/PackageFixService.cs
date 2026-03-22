using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using DirectumMcp.Core.Validators;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for fix_package. Used by MCP tool and pipeline.
/// </summary>
public class PackageFixService : IPipelineStep
{
    public string ToolName => "fix_package";

    public async Task<FixPackageResult> FixAsync(
        string packagePath,
        bool dryRun = true,
        CancellationToken ct = default)
    {
        var (workspace, error) = await PackageWorkspace.OpenAsync(packagePath, includeRuResx: true, ct: ct);
        if (workspace == null)
            return new FixPackageResult { Success = false, Errors = [error!], PackagePath = packagePath };

        using (workspace)
        {
            var validationResults = await PackageValidator.RunAllChecks(workspace);

            var autoFixed = new List<FixPackageResult.ChangeInfo>();
            var manualRequired = new List<FixPackageResult.ManualIssueInfo>();

            foreach (var r in validationResults.Where(r => !r.CanAutoFix))
                manualRequired.Add(new FixPackageResult.ManualIssueInfo(
                    r.CheckName, Path.GetFileName(r.FilePath ?? ""), r.Message));

            // Auto-fixable checks
            foreach (var c in await FixCheck3(workspace.Entities, dryRun, ct))
                autoFixed.Add(c);
            foreach (var c in await FixCheck4(workspace.Entities, dryRun, ct))
                autoFixed.Add(c);
            foreach (var c in await FixCheck5(workspace.Entities, dryRun, ct))
                autoFixed.Add(c);
            foreach (var c in await FixCheck6(workspace.ResxFiles, workspace.MtdFiles, dryRun, ct))
                autoFixed.Add(c);
            foreach (var c in await FixCheck14(workspace.Entities, dryRun, ct))
                autoFixed.Add(c);

            if (!dryRun && workspace.IsDatFile && autoFixed.Count > 0)
            {
                File.Delete(packagePath);
                ZipFile.CreateFromDirectory(workspace.WorkDir, packagePath);
            }

            var realAutoFixed = autoFixed
                .Where(c => !c.After.Contains("не удалось определить автоматически")).ToList();
            var unresolvable = autoFixed
                .Where(c => c.After.Contains("не удалось определить автоматически"))
                .Select(c => new FixPackageResult.ManualIssueInfo(c.CheckId, c.FileName, $"{c.Before} -> {c.After}"))
                .ToList();

            manualRequired.AddRange(unresolvable);

            return new FixPackageResult
            {
                Success = true,
                PackagePath = packagePath,
                AutoFixedCount = realAutoFixed.Count,
                ManualRequiredCount = manualRequired.Count,
                DryRun = dryRun,
                AutoFixed = realAutoFixed,
                ManualRequired = manualRequired
            };
        }
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        var path = parameters.TryGetValue("packagePath", out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? "" : "";
        var dry = !parameters.TryGetValue("dryRun", out var dryEl) || dryEl.ValueKind != JsonValueKind.False;
        return await FixAsync(path, dry, ct);
    }

    #region Check3 — Reserved Enum Names

    private static async Task<List<FixPackageResult.ChangeInfo>> FixCheck3(
        List<(string Path, JsonDocument Doc)> entities, bool dryRun, CancellationToken ct)
    {
        var changes = new List<FixPackageResult.ChangeInfo>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.GetStringPropSafe("Name");
            var fileChanges = new List<(string PropName, string OldVal, string NewVal)>();

            CollectReservedEnumChanges(root, "Properties", fileChanges);
            CollectReservedEnumChangesFromBlocks(root, fileChanges);

            if (fileChanges.Count > 0)
            {
                if (!dryRun)
                {
                    var jsonText = await File.ReadAllTextAsync(path, ct);
                    var jsonNode = JsonNode.Parse(jsonText);
                    FixReservedEnumNamesInNode(jsonNode?["Properties"]?.AsArray());
                    jsonText = jsonNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(path, jsonText, ct);
                }

                foreach (var (propName, oldVal, newVal) in fileChanges)
                    changes.Add(new FixPackageResult.ChangeInfo("Check3",
                        Path.GetFileName(path),
                        $"{entityName}.{propName}.DirectValues[].Name = \"{oldVal}\"",
                        $"\"{newVal}\""));
            }
        }
        return changes;
    }

    private static void FixReservedEnumNamesInNode(JsonArray? properties)
    {
        if (properties == null) return;
        foreach (var prop in properties)
        {
            var directValues = prop?["DirectValues"]?.AsArray();
            if (directValues == null) continue;
            foreach (var dv in directValues)
            {
                var name = dv?["Name"]?.GetValue<string>();
                if (name != null && PackageValidator.CSharpReservedWords.Contains(name))
                    dv!["Name"] = name + "Value";
            }
        }
    }

    private static void CollectReservedEnumChanges(JsonElement root, string arrayPropName,
        List<(string, string, string)> fileChanges)
    {
        if (!root.TryGetProperty(arrayPropName, out var props) || props.ValueKind != JsonValueKind.Array)
            return;
        foreach (var prop in props.EnumerateArray())
        {
            var typeName = prop.GetStringPropSafe("$type");
            if (!typeName.Contains("EnumPropertyMetadata") && !typeName.Contains("EnumBlockPropertyMetadata"))
                continue;
            var propName = prop.GetStringPropSafe("Name");
            CollectReservedValues(prop, propName, fileChanges);
        }
    }

    private static void CollectReservedEnumChangesFromBlocks(JsonElement root, List<(string, string, string)> fileChanges)
    {
        if (!root.TryGetProperty("Blocks", out var blocks) || blocks.ValueKind != JsonValueKind.Array)
            return;
        foreach (var block in blocks.EnumerateArray())
        {
            if (!block.TryGetProperty("OutProperties", out var outProps) || outProps.ValueKind != JsonValueKind.Array)
                continue;
            foreach (var op in outProps.EnumerateArray())
            {
                if (!op.GetStringPropSafe("$type").Contains("Enum")) continue;
                CollectReservedValues(op, op.GetStringPropSafe("Name"), fileChanges);
            }
        }
    }

    private static void CollectReservedValues(JsonElement prop, string propName,
        List<(string, string, string)> fileChanges)
    {
        if (!prop.TryGetProperty("DirectValues", out var vals) || vals.ValueKind != JsonValueKind.Array) return;
        foreach (var val in vals.EnumerateArray())
        {
            var valName = val.GetStringPropSafe("Name");
            if (PackageValidator.CSharpReservedWords.Contains(valName))
                fileChanges.Add((propName, valName, valName + "Value"));
        }
    }

    #endregion

    #region Check4 — Duplicate Codes

    private static async Task<List<FixPackageResult.ChangeInfo>> FixCheck4(
        List<(string Path, JsonDocument Doc)> entities, bool dryRun, CancellationToken ct)
    {
        var changes = new List<FixPackageResult.ChangeInfo>();
        var codesByBase = new Dictionary<string, List<(string EntityName, string PropName, string Code, string FilePath)>>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var entityName = root.GetStringPropSafe("Name");
            var baseGuid = root.GetStringPropSafe("BaseGuid");
            if (string.IsNullOrEmpty(baseGuid)) continue;
            if (!root.TryGetProperty("Properties", out var props) || props.ValueKind != JsonValueKind.Array) continue;

            foreach (var prop in props.EnumerateArray())
            {
                var code = prop.GetStringPropSafe("Code");
                if (string.IsNullOrEmpty(code)) continue;
                if (!codesByBase.ContainsKey(baseGuid)) codesByBase[baseGuid] = [];
                codesByBase[baseGuid].Add((entityName, prop.GetStringPropSafe("Name"), code, path));
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
                    var prefix = new string(item.EntityName.Where(char.IsUpper).Take(2).ToArray());
                    if (prefix.Length < 2)
                        prefix = item.EntityName.Length >= 2 ? item.EntityName[..2] : item.EntityName;

                    var newCode = prefix + item.Code;
                    var suffix = 2;
                    while (usedCodes.Contains(newCode)) { newCode = prefix + item.Code + "_" + suffix; suffix++; }
                    usedCodes.Add(newCode);

                    if (!fileFixes.ContainsKey(item.FilePath)) fileFixes[item.FilePath] = [];
                    fileFixes[item.FilePath].Add((item.Code, newCode));

                    changes.Add(new FixPackageResult.ChangeInfo("Check4",
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
                        if (matchingFix.OldCode != null) prop!["Code"] = matchingFix.NewCode;
                    }
                }
                jsonText = jsonNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, jsonText, ct);
            }
        }

        return changes;
    }

    #endregion

    #region Check5 — Constraints

    private static async Task<List<FixPackageResult.ChangeInfo>> FixCheck5(
        List<(string Path, JsonDocument Doc)> entities, bool dryRun, CancellationToken ct)
    {
        var changes = new List<FixPackageResult.ChangeInfo>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            var typeProp = root.GetStringPropSafe("$type");
            if (!typeProp.Contains("AssignmentMetadata") && !typeProp.Contains("NoticeMetadata")) continue;
            if (!root.TryGetProperty("AttachmentGroups", out var groups) || groups.ValueKind != JsonValueKind.Array) continue;

            var entityName = root.GetStringPropSafe("Name");
            var hasNonEmpty = false;

            foreach (var group in groups.EnumerateArray())
            {
                var isAssociated = group.TryGetProperty("IsAssociatedEntityGroup", out var iae) && iae.GetBoolean();
                if (!isAssociated) continue;
                if (group.TryGetProperty("Constraints", out var constraints) &&
                    constraints.ValueKind == JsonValueKind.Array && constraints.GetArrayLength() > 0)
                {
                    hasNonEmpty = true;
                    changes.Add(new FixPackageResult.ChangeInfo("Check5",
                        Path.GetFileName(path),
                        $"{entityName}.AttachmentGroups[\"{group.GetStringPropSafe("Name")}\"].Constraints ({constraints.GetArrayLength()})",
                        "[]"));
                }
            }

            if (hasNonEmpty && !dryRun)
            {
                var jsonText = await File.ReadAllTextAsync(path, ct);
                var jsonNode = JsonNode.Parse(jsonText);
                if (jsonNode?["AttachmentGroups"] is JsonArray groupsArray)
                {
                    foreach (var groupNode in groupsArray)
                    {
                        if (groupNode is not JsonObject groupObj) continue;
                        if (!(groupObj["IsAssociatedEntityGroup"]?.GetValue<bool>() ?? false)) continue;
                        if (groupObj["Constraints"] is JsonArray arr && arr.Count > 0)
                            groupObj["Constraints"] = new JsonArray();
                    }
                }
                await File.WriteAllTextAsync(path, jsonNode!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
            }
        }

        return changes;
    }

    #endregion

    #region Check6 — Resx Keys

    private static async Task<List<FixPackageResult.ChangeInfo>> FixCheck6(
        string[] resxFiles, string[] mtdFiles, bool dryRun, CancellationToken ct)
    {
        var changes = new List<FixPackageResult.ChangeInfo>();
        var mtdPropertyMap = await PackageValidator.BuildMtdPropertyMap(mtdFiles, ct);

        foreach (var resxFile in resxFiles)
        {
            var xml = await File.ReadAllTextAsync(resxFile, ct);
            var xdoc = XDocument.Parse(xml);
            var dataElements = xdoc.Descendants("data").ToList();
            var modified = false;

            var fileName = Path.GetFileNameWithoutExtension(resxFile);
            if (fileName.EndsWith(".ru", StringComparison.OrdinalIgnoreCase)) fileName = fileName[..^3];
            if (fileName.EndsWith("System", StringComparison.OrdinalIgnoreCase)) fileName = fileName[..^6];

            List<string>? propNames = null;
            if (!string.IsNullOrEmpty(fileName)) mtdPropertyMap.TryGetValue(fileName, out propNames);

            foreach (var data in dataElements)
            {
                var keyName = data.Attribute("name")?.Value ?? "";
                if (!PackageValidator.IsResourceGuidKey(keyName)) continue;

                var value = data.Element("value")?.Value ?? "";
                string? newKey = null;
                if (propNames != null)
                {
                    var match = propNames.FirstOrDefault(p => string.Equals(p, value, StringComparison.OrdinalIgnoreCase));
                    if (match != null) newKey = $"Property_{match}";
                }

                if (newKey != null)
                {
                    changes.Add(new FixPackageResult.ChangeInfo("Check6",
                        Path.GetFileName(resxFile), $"ключ `{keyName}` (значение: \"{value}\")", $"`{newKey}`"));
                    if (!dryRun) { data.SetAttributeValue("name", newKey); modified = true; }
                }
                else
                {
                    changes.Add(new FixPackageResult.ChangeInfo("Check6",
                        Path.GetFileName(resxFile), $"ключ `{keyName}` (значение: \"{value}\")",
                        "_(не удалось определить автоматически, требуется ручное исправление)_"));
                }
            }

            if (modified && !dryRun) xdoc.Save(resxFile);
        }

        return changes;
    }

    #endregion

    #region Check14 — DomainApi Version

    private static async Task<List<FixPackageResult.ChangeInfo>> FixCheck14(
        List<(string Path, JsonDocument Doc)> entities, bool dryRun, CancellationToken ct)
    {
        var changes = new List<FixPackageResult.ChangeInfo>();

        foreach (var (path, doc) in entities)
        {
            var root = doc.RootElement;
            if (root.TryGetProperty("IsAutoGenerated", out var iag) && iag.ValueKind == JsonValueKind.True) continue;

            bool hasDomainApi2 = false;
            if (root.TryGetProperty("Versions", out var versions) && versions.ValueKind == JsonValueKind.Array)
            {
                foreach (var ver in versions.EnumerateArray())
                {
                    if (ver.TryGetProperty("Type", out var typeEl) &&
                        string.Equals(typeEl.GetString(), "DomainApi", StringComparison.OrdinalIgnoreCase) &&
                        ver.TryGetProperty("Number", out var numEl) &&
                        numEl.TryGetInt32(out var num) && num >= 2)
                    { hasDomainApi2 = true; break; }
                }
            }

            if (hasDomainApi2) continue;

            var entityName = root.GetStringPropSafe("Name");
            changes.Add(new FixPackageResult.ChangeInfo("Check14",
                Path.GetFileName(path), $"{entityName}.Versions — нет DomainApi:2", "добавлен DomainApi:2"));

            if (!dryRun)
            {
                var jsonText = await File.ReadAllTextAsync(path, ct);
                var jsonNode = JsonNode.Parse(jsonText);
                if (jsonNode is JsonObject rootObj)
                {
                    var versionsArray = rootObj["Versions"]?.AsArray();
                    if (versionsArray == null) { versionsArray = new JsonArray(); rootObj["Versions"] = versionsArray; }
                    versionsArray.Add(new JsonObject { ["Type"] = "DomainApi", ["Number"] = 2 });
                    await File.WriteAllTextAsync(path, jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), ct);
                }
            }
        }

        return changes;
    }

    #endregion
}

internal static class JsonElementSafeExtensions
{
    public static string GetStringPropSafe(this JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? "" : "";
    }
}
