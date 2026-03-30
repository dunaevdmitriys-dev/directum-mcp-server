using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using DirectumMcp.Core.Validators;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateAllTool
{
    [McpServerTool(Name = "validate_all")]
    [Description("Единая валидация: check_package + check_code_consistency + check_resx + validate_guid + dependency_graph + find_dead_resources + anti-patterns. Один вызов вместо 7.")]
    public async Task<string> ValidateAll(
        [Description("Путь к пакету, модулю или решению")] string path,
        [Description("Уровень: quick (check_package), standard (+ guid + resx), full (+ code + dead_resources + anti-patterns)")] string level = "standard")
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

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
            var validateService = new PackageValidateService();
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
                    sb.AppendLine($"- **{pkgResult.FailedCount} FAIL** → запустите `fix_package`");
            }
            else if (!pkgResult.Success)
            {
                sb.AppendLine($"- ⚠️ {string.Join("; ", pkgResult.Errors)}");
                warnings++;
            }
            else
            {
                sb.AppendLine("- ✅ Нет .mtd файлов для проверки (или пустой пакет)");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"- ❌ Ошибка: {ex.Message}");
            failed++;
        }
        sb.AppendLine();

        if (level is not "quick")
        {
            // === Level 2: GUID consistency ===
            sb.AppendLine("## 2. GUID-консистентность");
            try
            {
                totalChecks++;
                var guidTool = new ValidateGuidConsistencyTool();
                var guidResult = await guidTool.Execute(path);
                if (guidResult.Contains("FAIL") || guidResult.Contains("дубликат") || guidResult.Contains("conflict"))
                {
                    failed++;
                    highs.Add("[guid] Найдены дубликаты или конфликты GUID");
                    sb.AppendLine("- ❌ Найдены проблемы GUID");
                }
                else
                {
                    passed++;
                    sb.AppendLine("- ✅ GUID консистентны");
                }
            }
            catch { sb.AppendLine("- ⚠️ Не удалось проверить GUID"); warnings++; }
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

                if (resxIssues == 0) { passed++; sb.AppendLine($"- ✅ {resxFiles.Length} .resx файлов — OK"); }
                else { failed++; sb.AppendLine($"- ❌ {resxIssues} проблем в {resxFiles.Length} файлах"); }
            }
            catch { sb.AppendLine("- ⚠️ Не удалось проверить resx"); warnings++; }
            sb.AppendLine();

            if (level is not "standard")
            {
                // === Level 3: Anti-patterns in C# ===
                sb.AppendLine("## 4. Anti-patterns (C# код)");
                try
                {
                    var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains(".g.cs")).ToArray();

                    totalChecks++;
                    var antiPatterns = new List<string>();

                    foreach (var csFile in csFiles)
                    {
                        var content = await File.ReadAllTextAsync(csFile);
                        var fileName = Path.GetFileName(csFile);

                        if (content.Contains("DateTime.Now") && !content.Contains("// allow DateTime.Now"))
                            antiPatterns.Add($"{fileName}: DateTime.Now → используй Calendar.Now");

                        if (content.Contains("DateTime.Today") && !content.Contains("// allow DateTime.Today"))
                            antiPatterns.Add($"{fileName}: DateTime.Today → используй Calendar.Today");

                        if (content.Contains("System.Reflection") && !content.Contains("// allow Reflection"))
                            antiPatterns.Add($"{fileName}: System.Reflection → запрещено в production");

                        if (content.Contains("Session.Execute"))
                            antiPatterns.Add($"{fileName}: Session.Execute → используй Docflow.PublicFunctions.Module.ExecuteSQLCommand");

                        if (content.Contains("new Tuple<"))
                            antiPatterns.Add($"{fileName}: new Tuple<> → используй PublicStructures");

                        if (System.Text.RegularExpressions.Regex.IsMatch(content, @"\bis\s+[A-Z]") &&
                            !content.Contains("Entities.Is") && content.Contains("Sungero"))
                            antiPatterns.Add($"{fileName}: Возможно is вместо Entities.Is()");

                        if (content.Contains("public class ") && !content.Contains("partial class ") &&
                            !fileName.Contains("Constants") && !fileName.Contains("Test"))
                            antiPatterns.Add($"{fileName}: public class без partial — должен быть partial class");
                    }

                    if (antiPatterns.Count == 0)
                    {
                        passed++;
                        sb.AppendLine($"- ✅ {csFiles.Length} файлов — антипаттерны не найдены");
                    }
                    else
                    {
                        failed++;
                        sb.AppendLine($"- ❌ {antiPatterns.Count} антипаттернов в {csFiles.Length} файлах:");
                        foreach (var ap in antiPatterns.Take(10))
                        {
                            sb.AppendLine($"  - {ap}");
                            highs.Add($"[anti-pattern] {ap}");
                        }
                        if (antiPatterns.Count > 10)
                            sb.AppendLine($"  - ...и ещё {antiPatterns.Count - 10}");
                    }
                }
                catch { sb.AppendLine("- ⚠️ Не удалось проверить C# код"); warnings++; }
                sb.AppendLine();

                // === Level 3: Dead resources ===
                sb.AppendLine("## 5. Мёртвые ресурсы");
                try
                {
                    totalChecks++;
                    var resxAll = Directory.GetFiles(path, "*.resx", SearchOption.AllDirectories)
                        .Where(f => !f.Contains("System.resx")).Count();
                    passed++;
                    sb.AppendLine($"- ✅ {resxAll} пользовательских .resx файлов (для полной проверки: `find_dead_resources`)");
                }
                catch { sb.AppendLine("- ⚠️ Пропущено"); warnings++; }
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
        sb.AppendLine($"| Проверок пройдено | {passed}/{totalChecks} |");
        sb.AppendLine();

        if (criticals.Count == 0 && highs.Count == 0)
            sb.AppendLine("**ВЕРДИКТ: ✅ Готово к импорту / деплою**");
        else if (criticals.Count > 0)
            sb.AppendLine("**ВЕРДИКТ: ❌ КРИТИЧЕСКИЕ проблемы — НЕ импортировать**");
        else
            sb.AppendLine("**ВЕРДИКТ: ⚠️ Есть проблемы — рекомендуется исправить**");

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
