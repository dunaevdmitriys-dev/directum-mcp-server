using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class InitializerGenerateServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InitializerGenerateService _service = new();
    private readonly ModuleScaffoldService _moduleService = new();

    public InitializerGenerateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "InitGenTests_" + Guid.NewGuid().ToString("N")[..8]);
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
        var r = await _moduleService.ScaffoldAsync(_tempDir, name, "DirRX");
        return r.ModulePath;
    }

    [Fact]
    public async Task Generate_WithRecords_CreatesInitializer()
    {
        var modulePath = await CreateModule();

        var result = await _service.GenerateAsync(
            modulePath, "DirRX.TestMod",
            records: "LossReason:Высокая цена|Выбрали конкурента|Нет бюджета");

        Assert.True(result.Success);
        Assert.Equal(3, result.RecordsCount);
        Assert.Equal(1, result.EntitiesCount);

        var initPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleInitializer.cs");
        var content = await File.ReadAllTextAsync(initPath);
        Assert.Contains("CreateOrUpdateLossReason", content);
        Assert.Contains("Высокая цена", content);
        Assert.Contains("Выбрали конкурента", content);
    }

    [Fact]
    public async Task Generate_WithRoles_CreatesRolesAndConstants()
    {
        var modulePath = await CreateModule();

        var result = await _service.GenerateAsync(
            modulePath, "DirRX.TestMod",
            roles: "Admin:Администратор:Роль администратора модуля");

        Assert.True(result.Success);
        Assert.Equal(1, result.RolesCount);

        var initPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleInitializer.cs");
        var content = await File.ReadAllTextAsync(initPath);
        Assert.Contains("CreateRoles", content);
        Assert.Contains("AdminRoleGuid", content);
        Assert.Contains("RoleName_Admin", content);

        var constPath = Path.Combine(modulePath, "DirRX.TestMod.Shared", "ModuleConstants.cs");
        var constContent = await File.ReadAllTextAsync(constPath);
        Assert.Contains("AdminRoleGuid", constContent);
    }

    [Fact]
    public async Task Generate_WithRoles_UpdatesResx()
    {
        var modulePath = await CreateModule();

        await _service.GenerateAsync(
            modulePath, "DirRX.TestMod",
            roles: "Manager:Менеджер:Роль менеджера продаж");

        var resxPath = Path.Combine(modulePath, "DirRX.TestMod.Shared", "Module.ru.resx");
        var content = await File.ReadAllTextAsync(resxPath);
        Assert.Contains("RoleName_Manager", content);
        Assert.Contains("Менеджер", content);
    }

    [Fact]
    public async Task Generate_MultipleEntities_AllCreated()
    {
        var modulePath = await CreateModule();

        var result = await _service.GenerateAsync(
            modulePath, "DirRX.TestMod",
            records: "Stage:Новая|В работе|Выиграна;LossReason:Цена|Конкурент");

        Assert.True(result.Success);
        Assert.Equal(2, result.EntitiesCount);
        Assert.Equal(5, result.RecordsCount);

        var initPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleInitializer.cs");
        var content = await File.ReadAllTextAsync(initPath);
        Assert.Contains("FillStage()", content);
        Assert.Contains("FillLossReason()", content);
    }

    [Fact]
    public async Task Generate_GrantRights_Generated()
    {
        var modulePath = await CreateModule();

        var result = await _service.GenerateAsync(
            modulePath, "DirRX.TestMod",
            records: "Item:A|B");

        var initPath = Path.Combine(modulePath, "DirRX.TestMod.Server", "ModuleInitializer.cs");
        var content = await File.ReadAllTextAsync(initPath);
        Assert.Contains("GrantRights", content);
        Assert.Contains("AccessRights.Grant", content);
    }

    [Fact]
    public async Task Generate_EmptyParams_Fails()
    {
        var result = await _service.GenerateAsync("", "");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Generate_ToMarkdown_ContainsInfo()
    {
        var modulePath = await CreateModule();
        var result = await _service.GenerateAsync(modulePath, "DirRX.TestMod",
            records: "X:A|B", roles: "R:Роль:Описание");

        var md = result.ToMarkdown();
        Assert.Contains("Инициализатор", md);
        Assert.Contains("Ролей", md);
    }
}
