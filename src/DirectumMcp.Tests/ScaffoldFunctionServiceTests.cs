using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class ScaffoldFunctionServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly FunctionScaffoldService _service = new();
    private readonly ModuleScaffoldService _moduleService = new();

    public ScaffoldFunctionServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ScaffoldFuncTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private async Task<string> CreateModule(string name = "TestMod")
    {
        var result = await _moduleService.ScaffoldAsync(_tempDir, name, "DirRX");
        return result.ModulePath;
    }

    [Fact]
    public async Task Scaffold_ServerFunction_CreatesFile()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "GetActiveDeals", "DirRX.TestMod",
            returnType: "void", side: "server");

        Assert.True(result.Success);
        Assert.Equal("GetActiveDeals", result.FunctionName);
        Assert.Equal("server", result.Side);
    }

    [Fact]
    public async Task Scaffold_ServerFunction_HasRemoteAttribute()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "MyFunc", "DirRX.TestMod",
            side: "server", isRemote: true);

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleServerFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("[Remote]", content);
        Assert.Contains("MyFunc", content);
    }

    [Fact]
    public async Task Scaffold_PublicFunction_HasPublicAttribute()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "GetData", "DirRX.TestMod",
            side: "server", isPublic: true);

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleServerFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("[Public]", content);
    }

    [Fact]
    public async Task Scaffold_PublicFunction_UpdatesModuleMtd()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "GetData", "DirRX.TestMod",
            returnType: "string", parameters: "entityId:long",
            side: "server", isPublic: true);

        Assert.True(result.Success);
        Assert.True(result.MtdUpdated);

        var mtdPath = Path.Combine(modulePath, "DirRX.TestMod.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("GetData", content);
        Assert.Contains("PublicFunctions", content);
        Assert.Contains("global::System.String", content);
        Assert.Contains("global::System.Int64", content);
    }

    [Fact]
    public async Task Scaffold_ClientFunction_HasLocalizeAttribute()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "ShowReport", "DirRX.TestMod",
            side: "client");

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.ClientBase", "ModuleClientFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("[LocalizeFunction(\"ShowReportFunctionName\"", content);
        Assert.Contains("virtual void ShowReport", content);
    }

    [Fact]
    public async Task Scaffold_WithParameters_GeneratesSignature()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "FilterDeals", "DirRX.TestMod",
            returnType: "void", parameters: "managerId:long,status:string",
            side: "server");

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleServerFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("long managerId", content);
        Assert.Contains("string status", content);
    }

    [Fact]
    public async Task Scaffold_WithBody_InsertsCode()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "GetCount", "DirRX.TestMod",
            returnType: "int", side: "server",
            body: "return 42;");

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleServerFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("return 42;", content);
    }

    [Fact]
    public async Task Scaffold_WithDescription_GeneratesXmlDoc()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "GetDeals", "DirRX.TestMod",
            side: "server", description: "Получить все активные сделки");

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleServerFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("/// Получить все активные сделки", content);
    }

    [Fact]
    public async Task Scaffold_EntityLevel_CreatesEntityFile()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "GetStatus", "DirRX.TestMod",
            entityName: "Deal", side: "server");

        Assert.True(result.Success);
        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "DealServerFunctions.cs");
        Assert.True(File.Exists(csPath));
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("partial class DealFunctions", content);
    }

    [Fact]
    public async Task Scaffold_MultipleFunctions_AppendsToFile()
    {
        var modulePath = await CreateModule();

        await _service.ScaffoldAsync(modulePath, "Func1", "DirRX.TestMod", side: "server");
        await _service.ScaffoldAsync(modulePath, "Func2", "DirRX.TestMod", side: "server");

        var csPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleServerFunctions.cs");
        var content = await File.ReadAllTextAsync(csPath);
        Assert.Contains("Func1", content);
        Assert.Contains("Func2", content);
    }

    [Fact]
    public async Task Scaffold_EmptyFunctionName_Fails()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "", "Mod");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Scaffold_InvalidSide_Fails()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "F", "Mod", side: "database");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Scaffold_ToMarkdown_ContainsInfo()
    {
        var modulePath = await CreateModule();

        var result = await _service.ScaffoldAsync(
            modulePath, "MyFunc", "DirRX.TestMod",
            side: "server", isPublic: true, isRemote: true);

        var md = result.ToMarkdown();
        Assert.Contains("MyFunc", md);
        Assert.Contains("server", md);
    }
}
