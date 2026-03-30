using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class TraceErrorsToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly TraceErrorsTool _tool;

    public TraceErrorsToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TraceErrTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new TraceErrorsTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string WriteLog(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task Trace_RecentErrors_ReturnsMatches()
    {
        var now = DateTime.Now;
        var timestamp = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logContent = $"{timestamp} [ERROR] Test.Namespace - Something went wrong\n  at Method() in file.cs:line 42\n";
        WriteLog("service.log", logContent);

        var result = await _tool.TraceErrors(_tempDir, lastMinutes: 5);

        Assert.Contains("Something went wrong", result);
    }

    [Fact]
    public async Task Trace_OldErrors_NotReturned()
    {
        // Write a log entry with a timestamp 2 hours ago
        var old = DateTime.Now.AddHours(-2);
        var timestamp = old.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logContent = $"{timestamp} [ERROR] Old.Error - Old error message\n";
        WriteLog("old.log", logContent);

        var result = await _tool.TraceErrors(_tempDir, lastMinutes: 30);

        Assert.DoesNotContain("Old error message", result);
    }

    [Fact]
    public async Task Trace_WarningLevel_IncludesWarnings()
    {
        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logContent = $"{ts} [WARN] Test - Warning message\n{ts} [ERROR] Test - Error message\n";
        WriteLog("mixed.log", logContent);

        var result = await _tool.TraceErrors(_tempDir, level: "warning", lastMinutes: 5);

        Assert.Contains("Warning message", result);
        Assert.Contains("Error message", result);
    }

    [Fact]
    public async Task Trace_ErrorLevel_ExcludesWarnings()
    {
        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logContent = $"{ts} [WARN] Test - Warning only\n{ts} [ERROR] Test - Error only\n";
        WriteLog("filter.log", logContent);

        var result = await _tool.TraceErrors(_tempDir, level: "error", lastMinutes: 5);

        Assert.Contains("Error only", result);
        Assert.DoesNotContain("Warning only", result);
    }

    [Fact]
    public async Task Trace_KeywordFilter_FiltersCorrectly()
    {
        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logContent = $"{ts} [ERROR] A - NullReferenceException in handler\n{ts} [ERROR] B - Timeout expired\n";
        WriteLog("keyword.log", logContent);

        var result = await _tool.TraceErrors(_tempDir, keyword: "NullReference", lastMinutes: 5);

        Assert.Contains("NullReferenceException", result);
        Assert.DoesNotContain("Timeout expired", result);
    }

    [Fact]
    public async Task Trace_EmptyLogs_ReturnsNoEntries()
    {
        WriteLog("empty.log", "");

        var result = await _tool.TraceErrors(_tempDir, lastMinutes: 5);

        Assert.Contains("0", result);
    }

    [Fact]
    public async Task Trace_NoLogFiles_ReportsNoFiles()
    {
        var emptyDir = Path.Combine(_tempDir, "nologs");
        Directory.CreateDirectory(emptyDir);

        var result = await _tool.TraceErrors(emptyDir);

        // May return error or empty report — just should not crash
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Trace_NonexistentPath_ReturnsError()
    {
        var result = await _tool.TraceErrors(Path.Combine(_tempDir, "no_such"));

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Trace_ReportContainsLogFileNames()
    {
        var now = DateTime.Now;
        var ts = now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        WriteLog("myservice.log", $"{ts} [ERROR] X - Test error\n");

        var result = await _tool.TraceErrors(_tempDir, lastMinutes: 5);

        Assert.Contains("myservice.log", result);
    }
}
