using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateReportTool
{
    // Patterns indicating hardcoded connection strings in .frx files
    private static readonly string[] ConnectionStringPatterns =
    [
        "Data Source=",
        "Server=",
        "ConnectionString",
        "Initial Catalog=",
    ];

    [McpServerTool(Name = "validate_report")]
    [Description("Валидация отчёта: .frx ↔ Queries.xml, датасеты, подключения.")]
    public async Task<string> ValidateReport(string path)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Путь не найден: `{path}`\nУкажите путь к папке отчёта или корню пакета.";

        var frxFiles = Directory.GetFiles(path, "*.frx", SearchOption.AllDirectories);

        if (frxFiles.Length == 0)
            return $"**Результат**: В директории `{path}` не найдено файлов .frx.";

        var sb = new StringBuilder();
        sb.AppendLine("# Валидация отчётов Directum RX");
        sb.AppendLine();
        sb.AppendLine($"**Путь**: `{path}`");
        sb.AppendLine($"**Найдено .frx файлов**: {frxFiles.Length}");
        sb.AppendLine();

        int totalIssues = 0;
        int totalReports = 0;

        foreach (var frxFile in frxFiles.OrderBy(f => f))
        {
            var reportDir = Path.GetDirectoryName(frxFile)!;
            var queriesFile = Path.Combine(reportDir, "Queries.xml");
            var reportName = Path.GetFileNameWithoutExtension(frxFile);

            var reportResult = await ValidateSingleReport(frxFile, queriesFile, reportName);
            totalReports++;
            totalIssues += reportResult.IssueCount;

            sb.AppendLine(reportResult.Markdown);
        }

        // Summary header — insert after the header lines
        var summary = totalIssues == 0
            ? $"**Итого**: {totalReports} отчётов проверено, проблем не обнаружено."
            : $"**Итого**: {totalReports} отчётов проверено, {totalIssues} проблем найдено.";

        // Insert summary before the first report section
        sb.Insert(sb.ToString().IndexOf("\n\n", sb.ToString().IndexOf("**Найдено") + 1) + 2, summary + "\n\n");

        return sb.ToString();
    }

    private static async Task<ReportResult> ValidateSingleReport(string frxFile, string queriesFile, string reportName)
    {
        var sb = new StringBuilder();
        int issueCount = 0;

        sb.AppendLine($"## Отчёт: `{reportName}`");
        sb.AppendLine($"Файл: `{frxFile}`");
        sb.AppendLine();

        // Parse .frx
        string frxContent;
        try
        {
            frxContent = await File.ReadAllTextAsync(frxFile);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**ОШИБКА**: Не удалось прочитать .frx файл: {ex.Message}");
            sb.AppendLine();
            return new ReportResult(sb.ToString(), 1);
        }

        XDocument frxDoc;
        try
        {
            frxDoc = XDocument.Parse(frxContent);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**ОШИБКА**: .frx файл не является корректным XML: {ex.Message}");
            sb.AppendLine();
            return new ReportResult(sb.ToString(), 1);
        }

        // Collect DataBand DataSource names from .frx
        var frxDataSources = frxDoc.Descendants("DataBand")
            .Select(e => e.Attribute("DataSource")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check 4: Empty template (no DataBand)
        bool hasDataBands = frxDoc.Descendants("DataBand").Any();
        if (!hasDataBands)
        {
            sb.AppendLine("### [WARN] Пустой шаблон");
            sb.AppendLine("В .frx файле не найдено ни одного элемента `DataBand`. Шаблон не содержит табличных данных.");
            sb.AppendLine();
            issueCount++;
        }

        // Check 2: Hardcoded connection strings
        var connectionIssues = new List<string>();
        foreach (var pattern in ConnectionStringPatterns)
        {
            if (frxContent.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                connectionIssues.Add($"`{pattern}`");
        }

        if (connectionIssues.Count > 0)
        {
            sb.AppendLine("### [FAIL] Хардкоженные строки подключения");
            sb.AppendLine("В .frx файле обнаружены признаки прямых строк подключения к БД:");
            foreach (var issue in connectionIssues)
                sb.AppendLine($"- {issue}");
            sb.AppendLine();
            sb.AppendLine("**Рекомендация**: Используйте именованные источники данных платформы Directum RX вместо прямых строк подключения.");
            sb.AppendLine();
            issueCount += connectionIssues.Count;
        }

        // Parse Queries.xml
        if (!File.Exists(queriesFile))
        {
            if (hasDataBands && frxDataSources.Count > 0)
            {
                sb.AppendLine("### [WARN] Queries.xml отсутствует");
                sb.AppendLine($"Файл `Queries.xml` не найден в `{Path.GetDirectoryName(frxFile)}`, но в шаблоне есть DataBand с источниками данных:");
                foreach (var ds in frxDataSources.OrderBy(s => s))
                    sb.AppendLine($"- `{ds}`");
                sb.AppendLine();
                issueCount++;
            }
            else if (!hasDataBands)
            {
                // Already reported as empty template, no extra message needed
            }
            else
            {
                sb.AppendLine("### [INFO] Queries.xml отсутствует");
                sb.AppendLine("Файл `Queries.xml` не найден. Если отчёт использует внешние запросы, создайте Queries.xml.");
                sb.AppendLine();
            }

            if (connectionIssues.Count == 0 && !hasDataBands)
                sb.AppendLine("**Нет дополнительных проблем.**");
            else if (issueCount == 0)
                sb.AppendLine("**Нет проблем.**");

            sb.AppendLine();
            return new ReportResult(sb.ToString(), issueCount);
        }

        // Parse queries from Queries.xml
        string queriesContent;
        try
        {
            queriesContent = await File.ReadAllTextAsync(queriesFile);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**ОШИБКА**: Не удалось прочитать Queries.xml: {ex.Message}");
            sb.AppendLine();
            return new ReportResult(sb.ToString(), issueCount + 1);
        }

        XDocument queriesDoc;
        try
        {
            queriesDoc = XDocument.Parse(queriesContent);
        }
        catch (Exception ex)
        {
            sb.AppendLine($"**ОШИБКА**: Queries.xml не является корректным XML: {ex.Message}");
            sb.AppendLine();
            return new ReportResult(sb.ToString(), issueCount + 1);
        }

        var queryNames = queriesDoc.Descendants("Query")
            .Select(e => e.Attribute("Name")?.Value)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Check 1: Dataset name mismatch — DataBand DataSource not found in Queries.xml
        var missingInQueries = frxDataSources
            .Where(ds => !queryNames.Contains(ds))
            .OrderBy(s => s)
            .ToList();

        if (missingInQueries.Count > 0)
        {
            sb.AppendLine("### [FAIL] Несовпадение датасетов (.frx → Queries.xml)");
            sb.AppendLine("DataBand/DataSource в .frx ссылаются на запросы, отсутствующие в Queries.xml:");
            foreach (var ds in missingInQueries)
                sb.AppendLine($"- `{ds}`");
            sb.AppendLine();
            sb.AppendLine("**Рекомендация**: Добавьте соответствующий элемент `<Query Name=\"...\">` в Queries.xml или исправьте атрибут `DataSource` в DataBand.");
            sb.AppendLine();
            issueCount += missingInQueries.Count;
        }

        // Check 3: Unused queries — queries in Queries.xml not referenced by any DataBand
        var unusedQueries = queryNames
            .Where(q => !frxDataSources.Contains(q))
            .OrderBy(s => s)
            .ToList();

        if (unusedQueries.Count > 0)
        {
            sb.AppendLine("### [WARN] Неиспользуемые запросы");
            sb.AppendLine("Запросы в Queries.xml не имеют ссылок ни в одном DataBand .frx шаблона:");
            foreach (var q in unusedQueries)
                sb.AppendLine($"- `{q}`");
            sb.AppendLine();
            sb.AppendLine("**Рекомендация**: Удалите неиспользуемые запросы из Queries.xml или добавьте соответствующие DataBand в шаблон.");
            sb.AppendLine();
            issueCount += unusedQueries.Count;
        }

        if (issueCount == 0)
        {
            sb.AppendLine("**Результат**: Проблем не обнаружено.");
            sb.AppendLine();
        }

        return new ReportResult(sb.ToString(), issueCount);
    }

    private record ReportResult(string Markdown, int IssueCount);
}
