namespace DirectumMcp.Core.Helpers;

/// <summary>
/// Thread-safe counter for tracking parse errors during file scanning operations.
/// Instead of silent catch { }, tools should increment this counter and report
/// the total at the end of their output.
/// </summary>
public class ParseErrorCounter
{
    private int _count;
    private readonly List<string> _details = new();
    private readonly int _maxDetails;

    /// <summary>
    /// Creates a new parse error counter.
    /// </summary>
    /// <param name="maxDetails">Maximum number of error details to retain (prevents memory bloat on large scans).</param>
    public ParseErrorCounter(int maxDetails = 10)
    {
        _maxDetails = maxDetails;
    }

    /// <summary>
    /// Number of parse errors encountered.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Whether any errors were encountered.
    /// </summary>
    public bool HasErrors => _count > 0;

    /// <summary>
    /// Records a parse error with an optional detail message.
    /// </summary>
    public void Record(string? filePath = null, string? message = null)
    {
        Interlocked.Increment(ref _count);

        if (_details.Count < _maxDetails)
        {
            var detail = !string.IsNullOrEmpty(filePath) && !string.IsNullOrEmpty(message)
                ? $"`{Path.GetFileName(filePath)}`: {message}"
                : !string.IsNullOrEmpty(filePath)
                    ? $"`{Path.GetFileName(filePath)}`"
                    : message ?? "unknown";

            lock (_details)
            {
                if (_details.Count < _maxDetails)
                    _details.Add(detail);
            }
        }
    }

    /// <summary>
    /// Appends a summary line to a StringBuilder, if any errors were recorded.
    /// Format: "Обработано N файлов, M пропущены с ошибкой"
    /// </summary>
    public void AppendSummary(System.Text.StringBuilder sb, int totalFiles)
    {
        if (_count == 0) return;

        sb.AppendLine();
        sb.AppendLine($"> **Обработано {totalFiles} файлов, {_count} пропущены с ошибкой парсинга**");

        if (_details.Count > 0)
        {
            foreach (var d in _details)
                sb.AppendLine($">   - {d}");

            if (_count > _details.Count)
                sb.AppendLine($">   - ...и ещё {_count - _details.Count}");
        }
    }

    /// <summary>
    /// Returns a summary string for inline use.
    /// </summary>
    public string ToSummary(int totalFiles)
    {
        if (_count == 0)
            return $"Обработано {totalFiles} файлов без ошибок.";

        return $"Обработано {totalFiles} файлов, {_count} пропущены с ошибкой парсинга.";
    }
}
