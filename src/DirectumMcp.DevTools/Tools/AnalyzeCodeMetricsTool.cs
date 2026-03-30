using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class AnalyzeCodeMetricsTool
{
    [McpServerTool(Name = "analyze_code_metrics")]
    [Description("Метрики качества C# кода: LOC, методы, сложность, anti-patterns, namespace, partial class. Code review помощник.")]
    public async Task<string> AnalyzeCodeMetrics(
        [Description("Путь к модулю или директории с .cs файлами")] string path,
        [Description("Порог сложности (макс. строк в методе, по умолчанию 50)")] int maxMethodLines = 50)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория не найдена: `{path}`";

        var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains(".g.cs"))
            .ToArray();

        if (csFiles.Length == 0)
            return "Нет .cs файлов для анализа.";

        var sb = new StringBuilder();
        sb.AppendLine("# Метрики качества кода");
        sb.AppendLine();

        int totalLines = 0, totalMethods = 0, totalClasses = 0;
        int antiPatterns = 0, longMethods = 0, missingPartial = 0, wrongNamespace = 0;
        var largestFiles = new List<(string Name, int Lines)>();
        var longMethodsList = new List<(string File, string Method, int Lines)>();
        var antiPatternsList = new List<(string File, string Pattern)>();

        foreach (var csFile in csFiles)
        {
            var content = await File.ReadAllTextAsync(csFile);
            var lines = content.Split('\n');
            var fileName = Path.GetFileName(csFile);
            var relativePath = Path.GetRelativePath(path, csFile);
            totalLines += lines.Length;
            largestFiles.Add((relativePath, lines.Length));

            // Count classes and methods
            var classMatches = Regex.Matches(content, @"\b(public|internal|private)\s+(partial\s+)?(sealed\s+)?(abstract\s+)?class\s+\w+");
            totalClasses += classMatches.Count;

            var methodMatches = Regex.Matches(content, @"\b(public|private|protected|internal)\s+(static\s+)?(virtual\s+)?(override\s+)?(async\s+)?[\w<>\[\],\s]+\s+(\w+)\s*\(");
            totalMethods += methodMatches.Count;

            // Check method length
            var methodStarts = new List<(int Line, string Name)>();
            for (int i = 0; i < lines.Length; i++)
            {
                var methodMatch = Regex.Match(lines[i], @"\b(public|private|protected)\s+.*\s+(\w+)\s*\(");
                if (methodMatch.Success && !lines[i].TrimStart().StartsWith("//"))
                    methodStarts.Add((i, methodMatch.Groups[2].Value));
            }

            for (int j = 0; j < methodStarts.Count; j++)
            {
                var startLine = methodStarts[j].Line;
                var endLine = j + 1 < methodStarts.Count ? methodStarts[j + 1].Line : lines.Length;
                var methodLen = endLine - startLine;

                if (methodLen > maxMethodLines)
                {
                    longMethods++;
                    longMethodsList.Add((relativePath, methodStarts[j].Name, methodLen));
                }
            }

            // Anti-patterns
            if (content.Contains("DateTime.Now") && !content.Contains("// allow"))
            { antiPatterns++; antiPatternsList.Add((relativePath, "DateTime.Now → Calendar.Now")); }

            if (content.Contains("DateTime.Today") && !content.Contains("// allow"))
            { antiPatterns++; antiPatternsList.Add((relativePath, "DateTime.Today → Calendar.Today")); }

            if (content.Contains("System.Reflection") && !content.Contains("// allow"))
            { antiPatterns++; antiPatternsList.Add((relativePath, "System.Reflection — запрещено")); }

            if (content.Contains("Session.Execute"))
            { antiPatterns++; antiPatternsList.Add((relativePath, "Session.Execute → ExecuteSQLCommand")); }

            if (content.Contains("new Tuple<"))
            { antiPatterns++; antiPatternsList.Add((relativePath, "new Tuple<> → PublicStructures")); }

            if (content.Contains("Thread.Sleep") || content.Contains("System.Threading.Thread"))
            { antiPatterns++; antiPatternsList.Add((relativePath, "Thread.Sleep → AsyncHandler")); }

            // Check partial class
            if (content.Contains("public class ") && !content.Contains("partial class ") &&
                !fileName.Contains("Constants") && !fileName.Contains("Test"))
            { missingPartial++; antiPatternsList.Add((relativePath, "public class без partial")); }

            // Check namespace
            if (fileName.Contains("Server") && content.Contains("namespace ") && !content.Contains(".Server"))
            { wrongNamespace++; antiPatternsList.Add((relativePath, "Server файл без .Server namespace")); }
        }

        // Summary
        sb.AppendLine("## Общие метрики");
        sb.AppendLine($"| Метрика | Значение |");
        sb.AppendLine($"|---------|---------|");
        sb.AppendLine($"| Файлов | {csFiles.Length} |");
        sb.AppendLine($"| Строк кода | {totalLines:N0} |");
        sb.AppendLine($"| Классов | {totalClasses} |");
        sb.AppendLine($"| Методов | {totalMethods} |");
        sb.AppendLine($"| Среднее строк/файл | {(csFiles.Length > 0 ? totalLines / csFiles.Length : 0)} |");
        sb.AppendLine($"| Среднее методов/файл | {(csFiles.Length > 0 ? totalMethods / csFiles.Length : 0)} |");
        sb.AppendLine();

        // Largest files
        sb.AppendLine("## Крупнейшие файлы (топ-10)");
        sb.AppendLine("| Файл | Строк |");
        sb.AppendLine("|------|-------|");
        foreach (var (name, loc) in largestFiles.OrderByDescending(f => f.Lines).Take(10))
            sb.AppendLine($"| {name} | {loc} |");
        sb.AppendLine();

        // Long methods
        if (longMethodsList.Count > 0)
        {
            sb.AppendLine($"## Длинные методы (>{maxMethodLines} строк): {longMethods}");
            sb.AppendLine("| Файл | Метод | Строк |");
            sb.AppendLine("|------|-------|-------|");
            foreach (var (file, method, lines) in longMethodsList.OrderByDescending(m => m.Lines).Take(15))
                sb.AppendLine($"| {file} | {method} | {lines} |");
            sb.AppendLine();
        }

        // Anti-patterns
        if (antiPatternsList.Count > 0)
        {
            sb.AppendLine($"## Anti-patterns: {antiPatternsList.Count}");
            sb.AppendLine("| Файл | Проблема |");
            sb.AppendLine("|------|---------|");
            foreach (var (file, pattern) in antiPatternsList.Take(20))
                sb.AppendLine($"| {file} | {pattern} |");
            sb.AppendLine();
        }

        // Score
        var score = 10.0;
        score -= antiPatterns * 0.5;
        score -= longMethods * 0.3;
        score -= missingPartial * 0.2;
        score -= wrongNamespace * 0.2;
        score = Math.Max(1, Math.Min(10, score));

        sb.AppendLine("## Оценка");
        sb.AppendLine($"**Качество кода: {score:F1}/10**");
        sb.AppendLine();

        if (score >= 8) sb.AppendLine("Отличный код. Минимальные замечания.");
        else if (score >= 6) sb.AppendLine("Хороший код. Есть замечания, но не критичные.");
        else if (score >= 4) sb.AppendLine("Средний код. Рекомендуется рефакторинг.");
        else sb.AppendLine("Плохой код. Требуется серьёзная доработка.");

        return sb.ToString();
    }
}
