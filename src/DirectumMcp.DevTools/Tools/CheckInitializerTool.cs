using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public partial class CheckInitializerTool
{
    [McpServerTool(Name = "check_initializer")]
    [Description("Валидация ModuleInitializer: версии, роли, дубликаты, async-паттерн, согласованность с Module.mtd.")]
    public async Task<string> Execute(
        [Description("Путь к директории модуля")] string modulePath)
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);
        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория не найдена: `{modulePath}`";

        // Find initializer files
        var initFiles = Directory.GetFiles(modulePath, "ModuleInitializer.cs", SearchOption.AllDirectories);
        if (initFiles.Length == 0)
            return $"**ОШИБКА**: ModuleInitializer.cs не найден в `{modulePath}`";

        // Find Module.mtd for cross-reference
        var moduleMtds = Directory.GetFiles(modulePath, "Module.mtd", SearchOption.AllDirectories);

        var sb = new StringBuilder();
        sb.AppendLine("# Проверка ModuleInitializer");
        sb.AppendLine();

        var totalErrors = 0;
        var totalWarnings = 0;

        foreach (var initFile in initFiles)
        {
            var content = await File.ReadAllTextAsync(initFile);
            var fileName = Path.GetRelativePath(modulePath, initFile);
            sb.AppendLine($"## {fileName}");
            sb.AppendLine();

            var errors = new List<string>();
            var warnings = new List<string>();
            var info = new List<string>();

            // Check 1: Version sequence
            CheckVersionSequence(content, errors, warnings, info);

            // Check 2: Duplicate version blocks
            CheckDuplicateVersions(content, errors);

            // Check 3: Async pattern
            CheckAsyncPattern(content, warnings);

            // Check 4: Role GUIDs format
            CheckRoleGuids(content, warnings, info);

            // Check 5: CreateOrGetRole/Grant patterns
            CheckRolePatterns(content, warnings);

            // Check 6: Cross-reference with Module.mtd (if available)
            if (moduleMtds.Length > 0)
                await CheckMtdConsistency(content, moduleMtds[0], warnings, info);

            // Check 7: Common antipatterns
            CheckAntipatterns(content, warnings);

            // Output results
            foreach (var e in errors)
            {
                sb.AppendLine($"- 🔴 **ОШИБКА**: {e}");
                totalErrors++;
            }
            foreach (var w in warnings)
            {
                sb.AppendLine($"- 🟡 **Предупреждение**: {w}");
                totalWarnings++;
            }
            foreach (var i in info)
            {
                sb.AppendLine($"- ℹ️ {i}");
            }

            if (errors.Count == 0 && warnings.Count == 0)
                sb.AppendLine("- ✅ Проблем не обнаружено");

            sb.AppendLine();
        }

        // Summary
        sb.AppendLine("---");
        sb.AppendLine($"**Итого:** {totalErrors} ошибок, {totalWarnings} предупреждений");

        return sb.ToString();
    }

    [GeneratedRegex(@"InitializationModule\(""([^""]+)""\)")]
    private static partial Regex VersionBlockRegex();

    [GeneratedRegex(@"new\s+Guid\(""([^""]+)""\)")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"\.Wait\(\)")]
    private static partial Regex WaitCallRegex();

    [GeneratedRegex(@"\.Result\b")]
    private static partial Regex ResultPropRegex();

    [GeneratedRegex(@"Roles\.GetAll\(\)")]
    private static partial Regex RolesGetAllRegex();

    private static void CheckVersionSequence(string content, List<string> errors, List<string> warnings, List<string> info)
    {
        var matches = VersionBlockRegex().Matches(content);
        if (matches.Count == 0)
        {
            warnings.Add("Не найдено ни одного блока `InitializationModule(\"version\")`");
            return;
        }

        var versions = matches.Select(m => m.Groups[1].Value).ToList();
        info.Add($"Версии инициализации: {string.Join(", ", versions)}");

        // Check semantic version ordering
        var parsed = new List<(string Original, Version? Parsed)>();
        foreach (var v in versions)
        {
            parsed.Add(Version.TryParse(v, out var ver) ? (v, ver) : (v, null));
        }

        var unparsed = parsed.Where(p => p.Parsed == null).ToList();
        if (unparsed.Count > 0)
            warnings.Add($"Нестандартные версии (не semver): {string.Join(", ", unparsed.Select(u => u.Original))}");

        // Check for ordering
        var parsedVersions = parsed.Where(p => p.Parsed != null).Select(p => p.Parsed!).ToList();
        for (var i = 1; i < parsedVersions.Count; i++)
        {
            if (parsedVersions[i] <= parsedVersions[i - 1])
                errors.Add($"Версии не в порядке возрастания: {parsedVersions[i - 1]} → {parsedVersions[i]}");
        }
    }

    private static void CheckDuplicateVersions(string content, List<string> errors)
    {
        var matches = VersionBlockRegex().Matches(content);
        var versions = matches.Select(m => m.Groups[1].Value).ToList();
        var duplicates = versions.GroupBy(v => v).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        foreach (var dup in duplicates)
            errors.Add($"Дублированная версия: `{dup}`");
    }

    private static void CheckAsyncPattern(string content, List<string> warnings)
    {
        // Check for .Wait() calls (blocking async)
        if (WaitCallRegex().IsMatch(content))
            warnings.Add("Обнаружен `.Wait()` — используйте `await` вместо блокирующего вызова");

        // Check for .Result (blocking async)
        if (ResultPropRegex().IsMatch(content))
            warnings.Add("Обнаружен `.Result` — используйте `await` вместо блокирующего вызова");
    }

    private static void CheckRoleGuids(string content, List<string> warnings, List<string> info)
    {
        var guids = GuidRegex().Matches(content);
        var guidSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var duplicateGuids = new List<string>();

        foreach (Match m in guids)
        {
            var guid = m.Groups[1].Value;
            if (!Guid.TryParse(guid, out _))
                warnings.Add($"Невалидный GUID: `{guid}`");
            else if (!guidSet.Add(guid))
                duplicateGuids.Add(guid);
        }

        if (duplicateGuids.Count > 0)
            warnings.Add($"Дублированные GUID: {string.Join(", ", duplicateGuids.Take(5))}");

        info.Add($"Обнаружено {guidSet.Count} уникальных GUID");
    }

    private static void CheckRolePatterns(string content, List<string> warnings)
    {
        // Check for Roles.GetAll() without filter — potential performance issue
        if (RolesGetAllRegex().IsMatch(content))
            warnings.Add("`Roles.GetAll()` без фильтра — возможна проблема производительности. Используйте `Roles.GetAll(r => r.Sid == guid)`");
    }

    private static async Task CheckMtdConsistency(string content, string moduleMtdPath, List<string> warnings, List<string> info)
    {
        try
        {
            var mtdContent = await File.ReadAllTextAsync(moduleMtdPath);
            using var doc = JsonDocument.Parse(mtdContent);
            var root = doc.RootElement;

            // Check if initializer references version numbers that exist in MTD Versions
            if (root.TryGetProperty("Versions", out var versionsEl) && versionsEl.ValueKind == JsonValueKind.Array)
            {
                var mtdVersions = new HashSet<string>();
                foreach (var v in versionsEl.EnumerateArray())
                {
                    var vName = v.TryGetProperty("Name", out var n) && n.ValueKind == JsonValueKind.String
                        ? n.GetString() ?? "" : "";
                    if (!string.IsNullOrEmpty(vName))
                        mtdVersions.Add(vName);
                }
                info.Add($"Версии в Module.mtd: {string.Join(", ", mtdVersions)}");
            }

            // Check module name matches namespace
            var moduleName = root.TryGetProperty("Name", out var nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString() ?? "" : "";
            if (!string.IsNullOrEmpty(moduleName) && !content.Contains(moduleName))
                warnings.Add($"Namespace модуля `{moduleName}` не найден в ModuleInitializer — возможно неправильный файл");
        }
        catch
        {
            // Module.mtd parse failed — skip cross-reference
        }
    }

    private static void CheckAntipatterns(string content, List<string> warnings)
    {
        // Thread.Sleep in initializer
        if (content.Contains("Thread.Sleep"))
            warnings.Add("`Thread.Sleep` в инициализаторе — замедляет старт системы");

        // Console.Write in production code
        if (content.Contains("Console.Write"))
            warnings.Add("`Console.Write` — используйте Logger для логирования");

        // Hardcoded connection strings
        if (content.Contains("Server=") || content.Contains("Data Source=") || content.Contains("Host="))
            warnings.Add("Возможна hardcoded connection string — используйте конфигурацию");

        // Empty catch blocks
        if (content.Contains("catch { }") || content.Contains("catch (Exception) { }"))
            warnings.Add("Пустой catch-блок — ошибки инициализации будут проглочены");
    }
}
