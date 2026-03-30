using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class EmailIntegrationCheckTool
{
    [McpServerTool(Name = "email_integration_check")]
    [Description("Проверить конфигурацию Email/DCS-интеграции: regex-паттерны, обработчики входящих, маршрутизация, SMTP.")]
    public async Task<string> CheckEmailIntegration(
        [Description("Путь к модулю или решению")] string path)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        var sb = new StringBuilder();
        sb.AppendLine("# Проверка Email/DCS интеграции");
        sb.AppendLine();

        int issues = 0;

        // 1. Search for email-related patterns in C# files
        var csFiles = Directory.Exists(path)
            ? Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("obj") && !f.Contains("bin")).ToArray()
            : Array.Empty<string>();

        // Email patterns
        var emailPatterns = new Dictionary<string, int>();
        var regexPatterns = new List<string>();
        var smtpUsage = new List<string>();

        foreach (var csFile in csFiles)
        {
            var content = await File.ReadAllTextAsync(csFile);
            var fileName = Path.GetFileName(csFile);

            // Check for email regex
            var regexMatches = Regex.Matches(content, @"new\s+Regex\s*\(\s*@?""([^""]+)""");
            foreach (Match m in regexMatches)
            {
                var pattern = m.Groups[1].Value;
                if (pattern.Contains("@") || pattern.Contains("mail", StringComparison.OrdinalIgnoreCase) ||
                    pattern.Contains("email", StringComparison.OrdinalIgnoreCase) ||
                    pattern.Contains("subject", StringComparison.OrdinalIgnoreCase))
                {
                    regexPatterns.Add($"{fileName}: `{pattern}`");
                }
            }

            // Check for SMTP
            if (content.Contains("SmtpClient") || content.Contains("MailMessage") || content.Contains("System.Net.Mail"))
                smtpUsage.Add(fileName);

            // Check for DCS references
            if (content.Contains("DocumentCaptureService") || content.Contains("DCS") || content.Contains("IncomingEmail"))
                emailPatterns[fileName] = emailPatterns.GetValueOrDefault(fileName) + 1;

            // Check for email parsing
            if (content.Contains("MailAddress") || content.Contains("email", StringComparison.OrdinalIgnoreCase))
                emailPatterns[fileName] = emailPatterns.GetValueOrDefault(fileName) + 1;
        }

        // 2. Check Module.mtd for email-related AsyncHandlers/Jobs
        var mtdFiles = Directory.Exists(path)
            ? Directory.GetFiles(path, "Module.mtd", SearchOption.AllDirectories)
            : Array.Empty<string>();

        var emailHandlers = new List<string>();
        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("AsyncHandlers", out var ah) && ah.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in ah.EnumerateArray())
                    {
                        var name = handler.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                        if (name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Mail", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Message", StringComparison.OrdinalIgnoreCase))
                        {
                            emailHandlers.Add(name);
                        }
                    }
                }

                if (root.TryGetProperty("Jobs", out var jobs) && jobs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var job in jobs.EnumerateArray())
                    {
                        var name = job.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                        if (name.Contains("Email", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Mail", StringComparison.OrdinalIgnoreCase))
                        {
                            emailHandlers.Add($"Job: {name}");
                        }
                    }
                }
            }
            catch { }
        }

        // Report
        sb.AppendLine("## Regex-паттерны для email");
        if (regexPatterns.Count > 0)
            foreach (var p in regexPatterns) sb.AppendLine($"- {p}");
        else { sb.AppendLine("_(не найдены)_"); issues++; }
        sb.AppendLine();

        sb.AppendLine("## AsyncHandlers/Jobs для email");
        if (emailHandlers.Count > 0)
            foreach (var h in emailHandlers) sb.AppendLine($"- {h}");
        else { sb.AppendLine("_(не найдены)_"); issues++; }
        sb.AppendLine();

        sb.AppendLine("## SMTP использование");
        if (smtpUsage.Count > 0)
            foreach (var s in smtpUsage) sb.AppendLine($"- {s}");
        else sb.AppendLine("_(не используется — только входящие?)_");
        sb.AppendLine();

        sb.AppendLine("## Файлы с email-логикой");
        if (emailPatterns.Count > 0)
            foreach (var (file, count) in emailPatterns.OrderByDescending(x => x.Value))
                sb.AppendLine($"- {file} ({count} упоминаний)");
        else sb.AppendLine("_(не найдены)_");
        sb.AppendLine();

        sb.AppendLine("---");
        sb.AppendLine($"**Проблем:** {issues}");
        if (issues > 0)
            sb.AppendLine("**Рекомендация:** Используй scaffold_async_handler для создания email-обработчика. См. directum://knowledge/integration-patterns и directum://knowledge/architecture-patterns");

        return sb.ToString();
    }
}
