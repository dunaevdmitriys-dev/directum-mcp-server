using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class GenerateRouteSchemeTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModuleScaffoldService _moduleService = new();

    public GenerateRouteSchemeTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RouteSchemeTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task GenerateRoute_DefaultBlocks_CreatesFiles()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "WF", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.GenerateRouteSchemeTool();
        var result = await tool.GenerateRouteScheme(mod.ModulePath, "DirRX.WF", "ApprovalTask");

        Assert.Contains("RouteScheme создана", result);
        Assert.Contains("ApprovalTask", result);
        Assert.Contains("4", result); // default 4 blocks
    }

    [Fact]
    public async Task GenerateRoute_CreatesRouteSchemeXml()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "RS", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.GenerateRouteSchemeTool();
        await tool.GenerateRouteScheme(mod.ModulePath, "DirRX.RS", "MyTask");

        var xmlPath = Path.Combine(mod.ModulePath, "DirRX.RS.Shared", "MyTask", "RouteScheme.xml");
        Assert.True(File.Exists(xmlPath));
        var content = await File.ReadAllTextAsync(xmlPath);
        Assert.Contains("<RouteScheme", content);
        Assert.Contains("<Block", content);
        Assert.Contains("<Transition", content);
    }

    [Fact]
    public async Task GenerateRoute_CreatesBlockHandlers()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "BH", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.GenerateRouteSchemeTool();
        await tool.GenerateRouteScheme(mod.ModulePath, "DirRX.BH", "Review");

        var csPath = Path.Combine(mod.ModulePath, "DirRX.BH.Server", "ReviewBlockHandlers.cs");
        Assert.True(File.Exists(csPath));
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("partial class ReviewBlockHandlers", content);
        Assert.Contains("Execute", content);
    }

    [Fact]
    public async Task GenerateRoute_CustomBlocks()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "CB", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.GenerateRouteSchemeTool();
        var result = await tool.GenerateRouteScheme(mod.ModulePath, "DirRX.CB", "Escalation",
            blocks: "Init:script;Assign:assignment;Review:script;Notify:notice",
            transitions: "Init→Assign;Assign→Review;Review→Notify");

        Assert.Contains("[script] **Init**", result);
        Assert.Contains("[assignment] **Assign**", result);
        Assert.Contains("[notice] **Notify**", result);
        Assert.Contains("Init → Assign", result);
    }

    [Fact]
    public async Task GenerateRoute_UpdatesModuleMtd()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "UM", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.GenerateRouteSchemeTool();
        await tool.GenerateRouteScheme(mod.ModulePath, "DirRX.UM", "Flow",
            blocks: "Start:script;End:script",
            transitions: "Start→End");

        var mtdPath = Path.Combine(mod.ModulePath, "DirRX.UM.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("ScriptBlockMetadata", content);
        Assert.Contains("Start", content);
    }

    [Fact]
    public async Task GenerateRoute_AssignmentBlock_HasStartHandler()
    {
        var mod = await _moduleService.ScaffoldAsync(_tempDir, "AH", "DirRX");
        var tool = new DirectumMcp.DevTools.Tools.GenerateRouteSchemeTool();
        await tool.GenerateRouteScheme(mod.ModulePath, "DirRX.AH", "Task",
            blocks: "CreateJob:assignment", transitions: "");

        var csPath = Path.Combine(mod.ModulePath, "DirRX.AH.Server", "TaskBlockHandlers.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("CreateJobStart", content);
        Assert.Contains("Performers", content);
    }
}
