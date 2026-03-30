using Xunit;

namespace DirectumMcp.Tests;

public class ScaffoldTaskToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly DirectumMcp.Core.Services.ModuleScaffoldService _moduleService = new();

    public ScaffoldTaskToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ScaffoldTaskTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task ScaffoldTask_CreatesThreeEntities()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "WF", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        var result = await tool.ScaffoldTask(mod.ModulePath, "ApprovalRequest", "DirRX.WF",
            russianName: "Запрос на согласование");

        Assert.Contains("Task + Assignment + Notice", result);
        Assert.Contains("ApprovalRequest", result);
        Assert.Contains("ApprovalRequestAssignment", result);
        Assert.Contains("ApprovalRequestNotice", result);
    }

    [Fact]
    public async Task ScaffoldTask_CreatesMtdFiles()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "TF", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        await tool.ScaffoldTask(mod.ModulePath, "Review", "DirRX.TF");

        Assert.True(File.Exists(Path.Combine(mod.ModulePath, "Review.mtd")));
        Assert.True(File.Exists(Path.Combine(mod.ModulePath, "ReviewAssignment.mtd")));
        Assert.True(File.Exists(Path.Combine(mod.ModulePath, "ReviewNotice.mtd")));
    }

    [Fact]
    public async Task ScaffoldTask_CreatesBlockHandlers()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "BH", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        await tool.ScaffoldTask(mod.ModulePath, "MyTask", "DirRX.BH");

        var blockPath = Path.Combine(mod.ModulePath, "Server", "MyTaskBlockHandlers.cs");
        Assert.True(File.Exists(blockPath));
        var content = await File.ReadAllTextAsync(blockPath);
        Assert.Contains("CreateAssignmentBlockStart", content);
        Assert.Contains("ProcessResultBlockExecute", content);
    }

    [Fact]
    public async Task ScaffoldTask_CreatesTaskHandlers()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "TH", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        await tool.ScaffoldTask(mod.ModulePath, "MyTask", "DirRX.TH");

        var handlerPath = Path.Combine(mod.ModulePath, "Server", "MyTaskHandlers.cs");
        Assert.True(File.Exists(handlerPath));
        var content = await File.ReadAllTextAsync(handlerPath);
        Assert.Contains("BeforeStart", content);
        Assert.Contains("BeforeAbort", content);
    }

    [Fact]
    public async Task ScaffoldTask_WithProperties()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "TP", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        var result = await tool.ScaffoldTask(mod.ModulePath, "Leave", "DirRX.TP",
            properties: "Employee:navigation,StartDate:date,EndDate:date",
            russianName: "Заявка на отпуск");

        var mtdPath = Path.Combine(mod.ModulePath, "Leave.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("Employee", content);
        Assert.Contains("StartDate", content);
        Assert.Contains("TaskMetadata", content);
    }

    [Fact]
    public async Task ScaffoldTask_WithoutNotice()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "NN", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        var result = await tool.ScaffoldTask(mod.ModulePath, "Simple", "DirRX.NN", createNotice: false);

        Assert.Contains("Simple", result);
        Assert.Contains("SimpleAssignment", result);
        Assert.DoesNotContain("SimpleNotice.mtd", result);
    }

    [Fact]
    public async Task ScaffoldTask_MtdHasCorrectBaseType()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "BT", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.ScaffoldTaskTool();
        await tool.ScaffoldTask(mod.ModulePath, "Check", "DirRX.BT");

        var taskMtd = await File.ReadAllTextAsync(Path.Combine(mod.ModulePath, "Check.mtd"));
        Assert.Contains("TaskMetadata", taskMtd);

        var assignMtd = await File.ReadAllTextAsync(Path.Combine(mod.ModulePath, "CheckAssignment.mtd"));
        Assert.Contains("AssignmentMetadata", assignMtd);

        var noticeMtd = await File.ReadAllTextAsync(Path.Combine(mod.ModulePath, "CheckNotice.mtd"));
        Assert.Contains("NoticeMetadata", noticeMtd);
    }
}
