using System.ComponentModel;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Validators;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidatePackageTool
{
    [McpServerTool(Name = "check_package")]
    [Description("Валидация .dat перед импортом в DDS: 8 проверок (коллекции, ссылки, enum, Code, resx, Analyzers, GUID-согласованность).")]
    public async Task<string> Validate(string packagePath)
    {
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

        var (workspace, error) = await PackageWorkspace.OpenAsync(packagePath);
        if (workspace == null)
            return error!;

        using (workspace)
        {
            var (results, mtdCount, resxCount) = await PackageValidator.RunAllChecksLegacy(workspace);
            return FormatReport(packagePath, mtdCount, resxCount, results);
        }
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
        if (failed > 0)
        {
            sb.AppendLine();
            sb.AppendLine("*Для автоисправления: `fix_package`*");
        }
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
}
