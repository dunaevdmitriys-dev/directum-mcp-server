using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class AnalyzeCodeMetricsTests : IDisposable
{
    private readonly string _tempDir;

    public AnalyzeCodeMetricsTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CodeMetricsTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Analyze_ReturnsMetrics()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Test.cs"),
            "using System;\nnamespace Test\n{\n    partial class Foo\n    {\n        public void Bar() { }\n    }\n}");

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("Метрики качества кода", result);
        Assert.Contains("Файлов", result);
        Assert.Contains("Строк кода", result);
        Assert.Contains("Оценка", result);
    }

    [Fact]
    public async Task Analyze_DetectsDateTimeNow()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Bad.cs"),
            "var now = DateTime.Now;");

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("DateTime.Now", result);
        Assert.Contains("Calendar.Now", result);
    }

    [Fact]
    public async Task Analyze_DetectsReflection()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Ref.cs"),
            "using System.Reflection;\nvar t = Assembly.Load(\"x\");");

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("System.Reflection", result);
    }

    [Fact]
    public async Task Analyze_DetectsLongMethods()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("namespace Test {");
        sb.AppendLine("    partial class Foo {");
        sb.AppendLine("        public void VeryLongMethod() {");
        for (int i = 0; i < 60; i++)
            sb.AppendLine($"            var x{i} = {i};");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Long.cs"), sb.ToString());

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir, maxMethodLines: 50);

        Assert.Contains("Длинные методы", result);
        Assert.Contains("VeryLongMethod", result);
    }

    [Fact]
    public async Task Analyze_CleanCode_HighScore()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Clean.cs"),
            "using System;\nnamespace Test.Server\n{\n    partial class CleanService\n    {\n        public void Process() { }\n    }\n}");

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("/10", result);
    }

    [Fact]
    public async Task Analyze_ShowsLargestFiles()
    {
        for (int i = 0; i < 5; i++)
        {
            var lines = string.Join("\n", Enumerable.Range(0, (i + 1) * 10).Select(x => $"// line {x}"));
            await File.WriteAllTextAsync(Path.Combine(_tempDir, $"File{i}.cs"), lines);
        }

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("Крупнейшие файлы", result);
    }

    [Fact]
    public async Task Analyze_EmptyDir_ReturnsMessage()
    {
        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("Нет .cs файлов", result);
    }

    [Fact]
    public async Task Analyze_DetectsSessionExecute()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "Sql.cs"),
            "Session.Execute(\"SELECT 1\");");

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("Session.Execute", result);
        Assert.Contains("ExecuteSQLCommand", result);
    }

    [Fact]
    public async Task Analyze_DetectsMissingPartial()
    {
        await File.WriteAllTextAsync(Path.Combine(_tempDir, "NoPartial.cs"),
            "namespace Test { public class Service { } }");

        var tool = new DirectumMcp.DevTools.Tools.AnalyzeCodeMetricsTool();
        var result = await tool.AnalyzeCodeMetrics(_tempDir);

        Assert.Contains("partial", result);
    }
}
