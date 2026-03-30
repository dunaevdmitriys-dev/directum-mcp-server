using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class TraceErrorsTool
{
    [McpServerTool(Name = "trace_errors")]
    [Description("Читать логи DDS/runtime, фильтр по уровню/ключевому слову/времени.")]
    public async Task<string> TraceErrors(
        [Description("Путь к директории с лог-файлами")] string logsPath,
        [Description("Фильтр по уровню: \"error\", \"warning\", \"all\" (по умолчанию \"error\")")] string level = "error",
        [Description("Показать записи за последние N минут (по умолчанию 60)")] int lastMinutes = 60,
        [Description("Дополнительный фильтр по ключевому слову (например, \"NullReference\", \"Missing area\", имя сущности)")] string? keyword = null,
        [Description("Максимальное количество записей для возврата (по умолчанию 30)")] int maxEntries = 30)
    {
        if (!PathGuard.IsAllowed(logsPath))
            return PathGuard.DenyMessage(logsPath);

        if (!Directory.Exists(logsPath))
            return $"**ОШИБКА**: Директория `{logsPath}` не существует.";

        var normalizedLevel = level.Trim().ToLowerInvariant();
        if (normalizedLevel != "error" && normalizedLevel != "warning" && normalizedLevel != "all")
            return $"**ОШИБКА**: Недопустимое значение параметра level: `{level}`. Допустимые значения: \"error\", \"warning\", \"all\".";

        if (lastMinutes <= 0)
            return "**ОШИБКА**: Параметр lastMinutes должен быть положительным числом.";

        if (maxEntries <= 0)
            return "**ОШИБКА**: Параметр maxEntries должен быть положительным числом.";

        var since = DateTime.Now.AddMinutes(-lastMinutes);

        // Collect log files from root and one level of subdirectories
        var logFiles = CollectLogFiles(logsPath);

        if (logFiles.Count == 0)
            return $"**ОШИБКА**: В директории `{logsPath}` (и её подпапках) не найдено лог-файлов (*.log, *.txt).";

        // Sort by last write time descending, limit to newest 10
        logFiles = logFiles
            .OrderByDescending(f => new FileInfo(f).LastWriteTime)
            .Take(10)
            .ToList();

        var entries = new List<LogEntry>();
        var checkedFiles = new List<string>();

        foreach (var filePath in logFiles)
        {
            var fileName = Path.GetFileName(filePath);
            checkedFiles.Add(fileName);

            try
            {
                var fileEntries = await ReadLogEntriesAsync(filePath, normalizedLevel, since, keyword);
                entries.AddRange(fileEntries);
            }
            catch (Exception ex)
            {
                // Skip unreadable files, but note them
                checkedFiles[checkedFiles.Count - 1] = $"{fileName} (ошибка чтения: {ex.Message})";
            }
        }

        // Sort all collected entries by timestamp descending, take top maxEntries
        entries = entries
            .OrderByDescending(e => e.Timestamp ?? DateTime.MinValue)
            .Take(maxEntries)
            .ToList();

        return BuildReport(logsPath, normalizedLevel, lastMinutes, keyword, since, entries, checkedFiles);
    }

    private static List<string> CollectLogFiles(string logsPath)
    {
        var extensions = new[] { "*.log", "*.txt" };
        var files = new List<string>();

        foreach (var ext in extensions)
        {
            files.AddRange(Directory.GetFiles(logsPath, ext, SearchOption.TopDirectoryOnly));
        }

        // One level of subdirectories
        try
        {
            foreach (var subDir in Directory.GetDirectories(logsPath))
            {
                foreach (var ext in extensions)
                {
                    files.AddRange(Directory.GetFiles(subDir, ext, SearchOption.TopDirectoryOnly));
                }
            }
        }
        catch
        {
            // Ignore subdirectory enumeration errors
        }

        return files;
    }

    private static readonly Regex TimestampRegex = new(
        @"^(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?)",
        RegexOptions.Compiled);

    private static readonly Regex BracketTimestampRegex = new(
        @"^\[(\d{4}-\d{2}-\d{2}[T ]\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?)\]",
        RegexOptions.Compiled);

    private static readonly Regex LevelRegex = new(
        @"\[(ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE|FATAL|VERBOSE)\]|\b(ERROR|WARN(?:ING)?|INFO|DEBUG|TRACE|FATAL):",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd HH:mm:ss.fffffff",
        "yyyy-MM-dd HH:mm:ss.ffffff",
        "yyyy-MM-dd HH:mm:ss.fffff",
        "yyyy-MM-dd HH:mm:ss.ffff",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm:ss.ff",
        "yyyy-MM-dd HH:mm:ss.f",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fffffff",
        "yyyy-MM-ddTHH:mm:ss.ffffff",
        "yyyy-MM-ddTHH:mm:ss.fffff",
        "yyyy-MM-ddTHH:mm:ss.ffff",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm:ss.ff",
        "yyyy-MM-ddTHH:mm:ss.f",
        "yyyy-MM-ddTHH:mm:ss",
    ];

    private static async Task<List<LogEntry>> ReadLogEntriesAsync(
        string filePath,
        string normalizedLevel,
        DateTime since,
        string? keyword)
    {
        const int maxLines = 10000;
        var lines = new List<string>(maxLines);

        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        // Read all lines (up to maxLines from end)
        var allLines = new List<string>();
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            allLines.Add(line);
            if (allLines.Count > maxLines)
                allLines.RemoveAt(0);
        }

        lines = allLines;

        // Parse into log entries
        var entries = new List<LogEntry>();
        LogEntry? current = null;

        foreach (var rawLine in lines)
        {
            var parsed = TryParseLine(rawLine);

            if (parsed != null)
            {
                if (current != null)
                    entries.Add(current);
                current = parsed;
                current.SourceFile = Path.GetFileName(filePath);
            }
            else if (current != null)
            {
                // Stack trace / continuation line
                current.AdditionalLines.Add(rawLine);
            }
        }

        if (current != null)
            entries.Add(current);

        // Filter entries
        var result = new List<LogEntry>();
        foreach (var entry in entries)
        {
            if (entry.Timestamp.HasValue && entry.Timestamp.Value < since)
                continue;

            if (!MatchesLevel(entry.Level, normalizedLevel))
                continue;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var fullText = entry.Message + " " + string.Join(" ", entry.AdditionalLines);
                if (!fullText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            result.Add(entry);
        }

        return result;
    }

    private static LogEntry? TryParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

        DateTime? timestamp = null;
        string remainder = line;

        // Try [timestamp] format
        var bracketMatch = BracketTimestampRegex.Match(line);
        if (bracketMatch.Success)
        {
            timestamp = ParseTimestamp(bracketMatch.Groups[1].Value);
            remainder = line.Substring(bracketMatch.Length).TrimStart();
        }
        else
        {
            // Try bare timestamp at start
            var tsMatch = TimestampRegex.Match(line);
            if (tsMatch.Success)
            {
                timestamp = ParseTimestamp(tsMatch.Groups[1].Value);
                remainder = line.Substring(tsMatch.Length).TrimStart();
            }
        }

        // Extract level
        var levelMatch = LevelRegex.Match(remainder);
        if (!levelMatch.Success)
        {
            // No level found — not a log header line (could be stack trace)
            return null;
        }

        var levelRaw = (levelMatch.Groups[1].Value + levelMatch.Groups[2].Value).ToUpperInvariant();
        var level = NormalizeLevel(levelRaw);

        // Extract namespace/source and message
        var afterLevel = remainder.Substring(levelMatch.Index + levelMatch.Length).TrimStart(' ', '-', ':');

        string? source = null;
        string message = afterLevel;

        // Try to detect "Namespace - Message" pattern
        var dashIdx = afterLevel.IndexOf(" - ", StringComparison.Ordinal);
        if (dashIdx > 0 && dashIdx < 80)
        {
            source = afterLevel.Substring(0, dashIdx).Trim();
            message = afterLevel.Substring(dashIdx + 3).Trim();
        }

        return new LogEntry
        {
            Timestamp = timestamp,
            Level = level,
            Source = source,
            Message = message,
            RawLine = line
        };
    }

    private static DateTime? ParseTimestamp(string value)
    {
        if (DateTime.TryParseExact(value, TimestampFormats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None,
                out var dt))
            return dt;

        if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt2))
            return dt2;

        return null;
    }

    private static string NormalizeLevel(string raw)
    {
        return raw switch
        {
            "WARNING" => "WARN",
            "FATAL" => "ERROR",
            "VERBOSE" => "DEBUG",
            _ => raw
        };
    }

    private static bool MatchesLevel(string entryLevel, string filter)
    {
        return filter switch
        {
            "all" => true,
            "warning" => entryLevel is "WARN" or "WARNING" or "ERROR" or "FATAL",
            "error" => entryLevel is "ERROR" or "FATAL",
            _ => true
        };
    }

    private static string BuildReport(
        string logsPath,
        string level,
        int lastMinutes,
        string? keyword,
        DateTime since,
        List<LogEntry> entries,
        List<string> checkedFiles)
    {
        var sb = new StringBuilder();

        var levelDisplay = level switch
        {
            "error" => "error",
            "warning" => "warning+",
            "all" => "all",
            _ => level
        };

        var filterDesc = $"level={levelDisplay}, последние {lastMinutes} мин";
        if (!string.IsNullOrWhiteSpace(keyword))
            filterDesc += $", ключевое слово: \"{keyword}\"";

        sb.AppendLine("## Логи Directum RX");
        sb.AppendLine();
        sb.AppendLine($"**Путь:** `{logsPath}`");
        sb.AppendLine($"**Фильтр:** {filterDesc}");
        sb.AppendLine($"**Найдено:** {entries.Count} записей");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        if (entries.Count == 0)
        {
            sb.AppendLine("_Записей по заданным критериям не найдено._");
            sb.AppendLine();
        }
        else
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var tsDisplay = entry.Timestamp.HasValue
                    ? entry.Timestamp.Value.ToString("dd.MM.yyyy HH:mm:ss")
                    : "неизвестно";

                var sourceDisplay = string.IsNullOrWhiteSpace(entry.Source) ? entry.SourceFile ?? string.Empty : entry.Source;
                var headerSuffix = string.IsNullOrWhiteSpace(sourceDisplay) ? string.Empty : $" — {sourceDisplay}";

                sb.AppendLine($"### {i + 1}. [{entry.Level}] {tsDisplay}{headerSuffix}");
                sb.AppendLine("```");
                sb.AppendLine(entry.Message);

                foreach (var extra in entry.AdditionalLines)
                {
                    if (!string.IsNullOrWhiteSpace(extra))
                        sb.AppendLine(extra);
                }

                sb.AppendLine("```");
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();

        var filesDisplay = checkedFiles.Count > 0
            ? string.Join(", ", checkedFiles)
            : "—";
        sb.AppendLine($"**Файлы проверены:** {filesDisplay}");

        var periodEnd = DateTime.Now;
        sb.AppendLine($"**Период:** {since:dd.MM.yyyy HH:mm} — {periodEnd:dd.MM.yyyy HH:mm}");

        return sb.ToString();
    }

    private sealed class LogEntry
    {
        public DateTime? Timestamp { get; set; }
        public string Level { get; set; } = "INFO";
        public string? Source { get; set; }
        public string Message { get; set; } = string.Empty;
        public string RawLine { get; set; } = string.Empty;
        public string? SourceFile { get; set; }
        public List<string> AdditionalLines { get; } = new();
    }
}
