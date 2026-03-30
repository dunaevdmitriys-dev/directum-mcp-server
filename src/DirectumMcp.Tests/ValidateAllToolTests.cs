using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class ValidateAllToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModuleScaffoldService _moduleService = new();
    private readonly EntityScaffoldService _entityService = new();

    public ValidateAllToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ValidateAllTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ValidateAll_Quick_RunsCheckPackage()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "Test", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ValidateAllTool();
        var result = await tool.ValidateAll(mod.ModulePath, "quick");

        Assert.Contains("Структура пакета", result);
        Assert.Contains("Итого", result);
    }

    [Fact]
    public async Task ValidateAll_Standard_IncludesGuidAndResx()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "Std", "DirRX");
        await _entityService.ScaffoldAsync(mod.ModulePath, "Item", "DirRX.Std", russianName: "Элемент");

        var tool = new DirectumMcp.DevTools.Tools.ValidateAllTool();
        var result = await tool.ValidateAll(mod.ModulePath, "standard");

        Assert.Contains("GUID-консистентность", result);
        Assert.Contains("Ресурсные файлы", result);
    }

    [Fact]
    public async Task ValidateAll_Full_IncludesAntiPatterns()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "Full", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ValidateAllTool();
        var result = await tool.ValidateAll(mod.ModulePath, "full");

        Assert.Contains("Anti-patterns", result);
        Assert.Contains("Мёртвые ресурсы", result);
    }

    [Fact]
    public async Task ValidateAll_DetectsAntiPattern_DateTimeNow()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "AP", "DirRX");
        // Write file with DateTime.Now
        var csPath = Path.Combine(mod.ModulePath, "DirRX.AP.Server", "Bad.cs");
        await File.WriteAllTextAsync(csPath, "var now = DateTime.Now; // bad!");

        var tool = new DirectumMcp.DevTools.Tools.ValidateAllTool();
        var result = await tool.ValidateAll(mod.ModulePath, "full");

        Assert.Contains("DateTime.Now", result);
        Assert.Contains("Calendar.Now", result);
    }

    [Fact]
    public async Task ValidateAll_DetectsResourceGuidKeys()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "RK", "DirRX");
        var resxPath = Path.Combine(mod.ModulePath, "DirRX.RK.Shared", "TestSystem.ru.resx");
        await File.WriteAllTextAsync(resxPath,
            "<?xml version=\"1.0\"?><root><data name=\"Resource_12345678-abcd-ef01-2345-67890abcdef0\"><value>Test</value></data></root>");

        var tool = new DirectumMcp.DevTools.Tools.ValidateAllTool();
        var result = await tool.ValidateAll(mod.ModulePath, "standard");

        Assert.Contains("Resource_", result);
    }

    [Fact]
    public async Task ValidateAll_CleanModule_ProducesReport()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "Clean", "DirRX", "Чистый модуль");
        var tool = new DirectumMcp.DevTools.Tools.ValidateAllTool();
        var result = await tool.ValidateAll(mod.ModulePath, "standard");

        Assert.Contains("Итого", result);
        Assert.Contains("ВЕРДИКТ", result);
        Assert.Contains("Структура пакета", result);
    }
}
