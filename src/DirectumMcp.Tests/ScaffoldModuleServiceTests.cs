using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class ScaffoldModuleServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ModuleScaffoldService _service = new();

    public ScaffoldModuleServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ScaffoldModuleTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        // Allow temp dir
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Scaffold_CreatesModuleDirectory()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "HRManagement", "DirRX", "Управление персоналом");

        Assert.True(result.Success);
        Assert.True(Directory.Exists(Path.Combine(_tempDir, "DirRX.HRManagement")));
    }

    [Fact]
    public async Task Scaffold_GeneratesModuleMtd()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Sales", "DirRX");

        Assert.True(result.Success);
        var mtdPath = Path.Combine(_tempDir, "DirRX.Sales", "DirRX.Sales.Shared", "Module.mtd");
        Assert.True(File.Exists(mtdPath));

        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("\"ModuleMetadata\"", content);
        Assert.Contains("\"Sales\"", content);
        Assert.Contains("\"DirRX\"", content);
        Assert.Contains(result.ModuleGuid, content);
    }

    [Fact]
    public async Task Scaffold_GeneratesCorrectNamespaces()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "MyModule", "TestCo");

        Assert.True(result.Success);
        var mtdPath = Path.Combine(_tempDir, "TestCo.MyModule", "TestCo.MyModule.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);

        Assert.Contains("\"ServerNamespace\": \"TestCo.MyModule.Server\"", content);
        Assert.Contains("\"SharedNamespace\": \"TestCo.MyModule.Shared\"", content);
        Assert.Contains("\"ClientNamespace\": \"TestCo.MyModule.Client\"", content);
        Assert.Contains("\"InterfaceNamespace\": \"TestCo.MyModule\"", content);
    }

    [Fact]
    public async Task Scaffold_CreatesServerFiles()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX");

        Assert.True(result.Success);
        var serverDir = Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.Server");

        Assert.True(File.Exists(Path.Combine(serverDir, "ModuleServerFunctions.cs")));
        Assert.True(File.Exists(Path.Combine(serverDir, "ModuleHandlers.cs")));
        Assert.True(File.Exists(Path.Combine(serverDir, "ModuleInitializer.cs")));
        Assert.True(File.Exists(Path.Combine(serverDir, "ModuleJobs.cs")));
    }

    [Fact]
    public async Task Scaffold_CreatesClientBaseFiles()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX");

        Assert.True(result.Success);
        var clientDir = Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.ClientBase");
        Assert.True(File.Exists(Path.Combine(clientDir, "ModuleClientFunctions.cs")));
    }

    [Fact]
    public async Task Scaffold_CreatesResxFiles()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX", "Тестовый модуль");

        Assert.True(result.Success);
        var sharedDir = Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.Shared");

        Assert.True(File.Exists(Path.Combine(sharedDir, "ModuleSystem.resx")));
        Assert.True(File.Exists(Path.Combine(sharedDir, "ModuleSystem.ru.resx")));

        var ruContent = await File.ReadAllTextAsync(Path.Combine(sharedDir, "ModuleSystem.ru.resx"));
        Assert.Contains("Тестовый модуль", ruContent);
    }

    [Fact]
    public async Task Scaffold_CreatesAnalyzersDirectory()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX");

        Assert.True(result.Success);
        Assert.True(Directory.Exists(
            Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.Shared", ".sds", "Libraries", "Analyzers")));
    }

    [Fact]
    public async Task Scaffold_WithCoverGroups()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Sales", "DirRX",
            coverGroups: "Продажи,Аналитика,Настройки");

        Assert.True(result.Success);
        var mtdPath = Path.Combine(_tempDir, "DirRX.Sales", "DirRX.Sales.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);

        Assert.Contains("\"Groups\":", content);
        Assert.Contains("\"Продажи\"", content);
        Assert.Contains("\"Аналитика\"", content);

        var resxPath = Path.Combine(_tempDir, "DirRX.Sales", "DirRX.Sales.Shared", "ModuleSystem.ru.resx");
        var resx = await File.ReadAllTextAsync(resxPath);
        Assert.Contains("CoverGroup_Продажи", resx);
    }

    [Fact]
    public async Task Scaffold_WithDependencies()
    {
        var companyGuid = "d534e107-a54d-48ec-85ff-bc44d731a82f";
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX",
            dependencies: $"{companyGuid}");

        Assert.True(result.Success);
        var mtdPath = Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains(companyGuid, content);
    }

    [Fact]
    public async Task Scaffold_ModuleGuidIsUnique()
    {
        var r1 = await _service.ScaffoldAsync(_tempDir, "Mod1", "DirRX");
        var tempDir2 = Path.Combine(Path.GetTempPath(), "ScaffoldModuleTests2_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir2);
        try
        {
            var r2 = await _service.ScaffoldAsync(tempDir2, "Mod2", "DirRX");
            Assert.NotEqual(r1.ModuleGuid, r2.ModuleGuid);
        }
        finally
        {
            Directory.Delete(tempDir2, true);
        }
    }

    [Fact]
    public async Task Scaffold_InitializerHasCorrectNamespace()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "MyMod", "TestCo");

        var initPath = Path.Combine(_tempDir, "TestCo.MyMod", "TestCo.MyMod.Server", "ModuleInitializer.cs");
        var content = await File.ReadAllTextAsync(initPath);

        Assert.Contains("namespace TestCo.MyMod.Server", content);
        Assert.Contains("public partial class ModuleInitializer", content);
        Assert.Contains("FirstInitializing", content);
    }

    [Fact]
    public async Task Scaffold_ConstantsHasModuleGuid()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "MyMod", "DirRX");

        var constPath = Path.Combine(_tempDir, "DirRX.MyMod", "DirRX.MyMod.Shared", "ModuleConstants.cs");
        var content = await File.ReadAllTextAsync(constPath);

        Assert.Contains("ModuleGuid", content);
        Assert.Contains(result.ModuleGuid, content);
        Assert.Contains("DirRX.MyMod", content);
    }

    [Fact]
    public async Task Scaffold_NoCover_OmitsCoverSection()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "NoCover", "DirRX", hasCover: false);

        Assert.True(result.Success);
        var mtdPath = Path.Combine(_tempDir, "DirRX.NoCover", "DirRX.NoCover.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("\"Cover\": null", content);
    }

    [Fact]
    public async Task Scaffold_MtdHasVersions()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX");

        var mtdPath = Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("\"ModuleMetadata\"", content);
        Assert.Contains("\"Number\": 12", content);
        Assert.Contains("\"DomainApi\"", content);
        Assert.Contains("\"Number\": 3", content);
    }

    [Fact]
    public async Task Scaffold_MtdHasHandledEvents()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX");

        var mtdPath = Path.Combine(_tempDir, "DirRX.Test", "DirRX.Test.Shared", "Module.mtd");
        var content = await File.ReadAllTextAsync(mtdPath);
        Assert.Contains("InitializingServer", content);
    }

    [Fact]
    public async Task Scaffold_ReturnsCreatedFilesList()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX");

        Assert.True(result.Success);
        Assert.Contains(result.CreatedFiles, f => f.Contains("Module.mtd"));
        Assert.Contains(result.CreatedFiles, f => f.Contains("ModuleServerFunctions.cs"));
        Assert.Contains(result.CreatedFiles, f => f.Contains("ModuleClientFunctions.cs"));
        Assert.Contains(result.CreatedFiles, f => f.Contains("ModuleInitializer.cs"));
        Assert.Contains(result.CreatedFiles, f => f.Contains("ModuleSystem.ru.resx"));
        Assert.True(result.CreatedFiles.Count >= 12);
    }

    [Fact]
    public async Task Scaffold_ResultFullNameIsCorrect()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Sales", "DirRX");

        Assert.Equal("DirRX.Sales", result.FullName);
    }

    [Fact]
    public async Task Scaffold_EmptyModuleName_Fails()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "", "DirRX");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Scaffold_EmptyOutputPath_Fails()
    {
        var result = await _service.ScaffoldAsync("", "Test", "DirRX");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Scaffold_ToMarkdown_ContainsKey()
    {
        var result = await _service.ScaffoldAsync(_tempDir, "Test", "DirRX", "Тест");

        var md = result.ToMarkdown();
        Assert.Contains("Модуль создан", md);
        Assert.Contains("DirRX.Test", md);
        Assert.Contains(result.ModuleGuid, md);
        Assert.Contains("Module.mtd", md);
    }
}
