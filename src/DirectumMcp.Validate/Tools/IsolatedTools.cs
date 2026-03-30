using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace DirectumMcp.Validate.Tools;

[McpServerToolType]
public class IsolatedTools
{
    [McpServerTool(Name = "validate_isolated_areas")]
    [Description("Проверить IsolatedFunctions: контракты, параметры, return types. Для модулей использующих Aspose, ExcelDataReader и др.")]
    public async Task<string> ValidateIsolatedAreas(
        [Description("Путь к модулю")] string modulePath,
        [Description("Полное имя модуля")] string moduleName = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Валидация IsolatedAreas");
        sb.AppendLine();

        // Find Isolated project
        var isolatedDirs = Directory.GetDirectories(modulePath, "*.Isolated", SearchOption.TopDirectoryOnly);
        if (isolatedDirs.Length == 0)
        {
            sb.AppendLine("Isolated-проект не найден. IsolatedAreas не используются.");
            return sb.ToString();
        }

        int totalFunctions = 0, totalIssues = 0;

        foreach (var isolatedDir in isolatedDirs)
        {
            var dirName = Path.GetFileName(isolatedDir);
            sb.AppendLine($"## {dirName}");

            // Check .csproj for external references
            var csprojFiles = Directory.GetFiles(isolatedDir, "*.csproj");
            if (csprojFiles.Length > 0)
            {
                var csproj = await File.ReadAllTextAsync(csprojFiles[0]);
                var packageRefs = Regex.Matches(csproj, @"PackageReference Include=""([^""]+)"" Version=""([^""]+)""");
                if (packageRefs.Count > 0)
                {
                    sb.AppendLine("### NuGet пакеты");
                    foreach (Match m in packageRefs)
                        sb.AppendLine($"- {m.Groups[1].Value} v{m.Groups[2].Value}");
                    sb.AppendLine();
                }

                var dllRefs = Regex.Matches(csproj, @"Reference Include=""([^""]+)""");
                if (dllRefs.Count > 0)
                {
                    sb.AppendLine("### Сторонние DLL");
                    foreach (Match m in dllRefs)
                        sb.AppendLine($"- {m.Groups[1].Value}");
                    sb.AppendLine();
                }
            }

            // Find IsolatedFunctions
            var csFiles = Directory.GetFiles(isolatedDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("obj") && !f.Contains("bin"));

            foreach (var csFile in csFiles)
            {
                var content = await File.ReadAllTextAsync(csFile);
                var fileName = Path.GetFileName(csFile);

                // Find public methods
                var methods = Regex.Matches(content, @"public\s+(?:static\s+)?(?:virtual\s+)?(\S+)\s+(\w+)\s*\(([^)]*)\)");
                foreach (Match m in methods)
                {
                    totalFunctions++;
                    var returnType = m.Groups[1].Value;
                    var methodName = m.Groups[2].Value;
                    var parameters = m.Groups[3].Value;

                    sb.AppendLine($"### {fileName} → {methodName}");
                    sb.AppendLine($"- Return: `{returnType}`");
                    sb.AppendLine($"- Params: `{(string.IsNullOrWhiteSpace(parameters) ? "нет" : parameters)}`");

                    // Check for common issues
                    if (returnType.Contains("Task") && !content.Contains("async"))
                    {
                        sb.AppendLine("- **WARNING**: возвращает Task, но метод не async");
                        totalIssues++;
                    }

                    if (content.Contains("Sungero.") && !content.Contains("Sungero.Core"))
                    {
                        sb.AppendLine("- **WARNING**: ссылка на Sungero.* — Isolated не должен зависеть от платформы напрямую");
                        totalIssues++;
                    }

                    sb.AppendLine();
                }
            }
        }

        sb.AppendLine("---");
        sb.AppendLine($"**Isolated функций:** {totalFunctions}");
        sb.AppendLine($"**Проблем:** {totalIssues}");

        return sb.ToString();
    }
}
