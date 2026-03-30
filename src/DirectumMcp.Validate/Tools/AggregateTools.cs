using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Services;
using DirectumMcp.Core.Validators;
using DirectumMcp.Shared;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class AggregateTools
{
    [McpServerTool(Name = "validate_all")]
    [Description(
        "Единая валидация пакета: check_package + GUID consistency + resx + anti-patterns + isolated areas. " +
        "Один вызов вместо 7. Уровни: quick (структура), standard (+ guid + resx), full (+ код + anti-patterns). " +
        "Для отдельной проверки используй check_package, validate_guid_consistency и т.д.")]
    public async Task<string> ValidateAll(
        PackageValidateService validateService,
        [Description("Путь к пакету, модулю или решению")] string path,
        [Description("Уровень: quick, standard, full")] string level = "standard")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Полная валидация");
        sb.AppendLine();
        sb.AppendLine($"**Путь:** `{path}`");
        sb.AppendLine($"**Уровень:** {level}");
        sb.AppendLine();

        int totalChecks = 0, passed = 0, failed = 0, warnings = 0;
        var criticals = new List<string>();
        var highs = new List<string>();
        var warns = new List<string>();

        // === Level 1: check_package (always) ===
        sb.AppendLine("## 1. Структура пакета (check_package)");
        try
        {
            var pkgResult = await validateService.ValidateAsync(path);
            if (pkgResult.Checks.Count > 0)
            {
                foreach (var check in pkgResult.Checks)
                {
                    totalChecks++;
                    if (check.Passed) { passed++; }
                    else
                    {
                        failed++;
                        foreach (var issue in check.Issues)
                            criticals.Add($"[check_package] {check.Name}: {issue}");
                    }
                }
                sb.AppendLine($"- {pkgResult.PassedCount}/{pkgResult.PassedCount + pkgResult.FailedCount} проверок пройдено");
                if (pkgResult.FailedCount > 0)
                    sb.AppendLine($"- **{pkgResult.FailedCount} FAIL** — запустите `fix_package`");
            }
            else if (!pkgResult.Success)
            {
                sb.AppendLine($"- WARN: {string.Join("; ", pkgResult.Errors)}");
                warnings++;
            }
            else
            {
                sb.AppendLine("- OK: Нет .mtd файлов или пустой пакет");
            }
        }
        catch (Exception ex) { sb.AppendLine($"- ERROR: {ex.Message}"); failed++; }
        sb.AppendLine();

        if (level is not "quick")
        {
            // === Level 2: GUID consistency ===
            sb.AppendLine("## 2. GUID-консистентность");
            try
            {
                totalChecks++;
                var guidTool = new ConsistencyTools();
                var guidResult = await guidTool.ValidateGuidConsistency(path);
                if (guidResult.Contains("ERROR") || guidResult.Contains("FAIL"))
                {
                    failed++;
                    highs.Add("[guid] Найдены проблемы GUID");
                    sb.AppendLine("- FAIL: Найдены проблемы GUID");
                }
                else { passed++; sb.AppendLine("- OK: GUID консистентны"); }
            }
            catch { sb.AppendLine("- WARN: Не удалось проверить"); warnings++; }
            sb.AppendLine();

            // === Level 2: Resx validation ===
            sb.AppendLine("## 3. Ресурсные файлы (.resx)");
            try
            {
                totalChecks++;
                var resxFiles = Directory.GetFiles(path, "*System.ru.resx", SearchOption.AllDirectories);
                var resxIssues = 0;
                foreach (var resxFile in resxFiles)
                {
                    var content = await File.ReadAllTextAsync(resxFile);
                    if (content.Contains("Resource_"))
                    {
                        resxIssues++;
                        highs.Add($"[resx] {Path.GetFileName(resxFile)}: содержит Resource_GUID ключи");
                    }
                    if (!content.Contains("DisplayName"))
                    {
                        resxIssues++;
                        warns.Add($"[resx] {Path.GetFileName(resxFile)}: нет DisplayName");
                    }
                }
                if (resxIssues == 0) { passed++; sb.AppendLine($"- OK: {resxFiles.Length} .resx файлов"); }
                else { failed++; sb.AppendLine($"- FAIL: {resxIssues} проблем в {resxFiles.Length} файлах"); }
            }
            catch { sb.AppendLine("- WARN: Не удалось проверить"); warnings++; }
            sb.AppendLine();

            // === Level 2: Isolated Areas (P1 FIX — ранее отсутствовало) ===
            sb.AppendLine("## 4. Isolated Areas");
            try
            {
                totalChecks++;
                var isolatedDirs = Directory.GetDirectories(path, "*.Isolated*", SearchOption.AllDirectories);
                if (isolatedDirs.Length > 0)
                {
                    var isolatedIssues = 0;
                    foreach (var dir in isolatedDirs)
                    {
                        var csproj = Directory.GetFiles(dir, "*.csproj", SearchOption.TopDirectoryOnly).FirstOrDefault();
                        if (csproj != null)
                        {
                            var content = await File.ReadAllTextAsync(csproj);
                            // Проверяем TargetFramework — должен быть net8.0+ для Isolated
                            if (content.Contains("net6.0") || content.Contains("net7.0"))
                            {
                                isolatedIssues++;
                                highs.Add($"[isolated] {Path.GetFileName(dir)}: TargetFramework слишком старый для RX 26.1");
                            }
                        }
                    }
                    if (isolatedIssues == 0) { passed++; sb.AppendLine($"- OK: {isolatedDirs.Length} isolated area(s)"); }
                    else { failed++; sb.AppendLine($"- FAIL: {isolatedIssues} проблем"); }
                }
                else
                {
                    passed++;
                    sb.AppendLine("- OK: Isolated Areas не найдены (не используются)");
                }
            }
            catch { sb.AppendLine("- WARN: Не удалось проверить"); warnings++; }
            sb.AppendLine();

            if (level is "full")
            {
                // === Level 3: Anti-patterns ===
                sb.AppendLine("## 5. Anti-patterns (C#)");
                try
                {
                    var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains(".g.cs")).ToArray();
                    totalChecks++;
                    var antiPatterns = new List<string>();
                    foreach (var csFile in csFiles)
                    {
                        var content = await File.ReadAllTextAsync(csFile);
                        var fn = Path.GetFileName(csFile);
                        if (content.Contains("DateTime.Now") && !content.Contains("// allow DateTime.Now"))
                            antiPatterns.Add($"{fn}: DateTime.Now — используй Calendar.Now");
                        if (content.Contains("DateTime.Today") && !content.Contains("// allow DateTime.Today"))
                            antiPatterns.Add($"{fn}: DateTime.Today — используй Calendar.Today");
                        if (content.Contains("Session.Execute"))
                            antiPatterns.Add($"{fn}: Session.Execute — используй PublicFunctions.Module.ExecuteSQLCommand");
                    }
                    if (antiPatterns.Count == 0) { passed++; sb.AppendLine($"- OK: {csFiles.Length} файлов"); }
                    else
                    {
                        failed++;
                        sb.AppendLine($"- FAIL: {antiPatterns.Count} антипаттернов:");
                        foreach (var ap in antiPatterns.Take(10))
                        {
                            sb.AppendLine($"  - {ap}");
                            highs.Add($"[anti-pattern] {ap}");
                        }
                    }
                }
                catch { sb.AppendLine("- WARN: Не удалось проверить"); warnings++; }
                sb.AppendLine();
            }
        }

        // === ИТОГО ===
        sb.AppendLine("---");
        sb.AppendLine("## Итого");
        sb.AppendLine();
        sb.AppendLine($"| Уровень | Количество |");
        sb.AppendLine($"|---------|-----------|");
        sb.AppendLine($"| CRITICAL | {criticals.Count} |");
        sb.AppendLine($"| HIGH | {highs.Count} |");
        sb.AppendLine($"| WARN | {warns.Count} |");
        sb.AppendLine($"| Проверок | {passed}/{totalChecks} |");
        sb.AppendLine();

        if (criticals.Count == 0 && highs.Count == 0)
            sb.AppendLine("**ВЕРДИКТ: OK — Готово к импорту**");
        else if (criticals.Count > 0)
            sb.AppendLine("**ВЕРДИКТ: FAIL — КРИТИЧЕСКИЕ проблемы**");
        else
            sb.AppendLine("**ВЕРДИКТ: WARN — Рекомендуется исправить**");

        if (criticals.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### CRITICAL");
            foreach (var c in criticals) sb.AppendLine($"- {c}");
        }
        if (highs.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### HIGH");
            foreach (var h in highs) sb.AppendLine($"- {h}");
        }

        return sb.ToString();
    }
}
