using DirectumMcp.Core.Services;
using Xunit;

namespace DirectumMcp.Tests;

public class PreviewCardServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly PreviewCardService _service = new();
    private readonly EntityScaffoldService _entityService = new();

    public PreviewCardServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "PreviewCardTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task Preview_ShowsEntityName()
    {
        await _entityService.ScaffoldAsync(_tempDir, "Deal", "DirRX.CRM",
            properties: "Name:string,Amount:double", russianName: "Сделка");

        var result = await _service.PreviewAsync(Path.Combine(_tempDir, "Deal.mtd"));

        Assert.True(result.Success);
        Assert.Equal("Deal", result.EntityName);
        Assert.Equal("DatabookEntry", result.BaseType);
    }

    [Fact]
    public async Task Preview_ShowsProperties()
    {
        await _entityService.ScaffoldAsync(_tempDir, "Item", "Mod",
            properties: "Name:string,Count:int,Active:bool");

        var result = await _service.PreviewAsync(Path.Combine(_tempDir, "Item.mtd"));

        Assert.True(result.Success);
        Assert.Equal(3, result.Properties.Count);
        Assert.Contains(result.Properties, p => p.Name == "Name");
        Assert.Contains(result.Properties, p => p.Name == "Count");
        Assert.Contains(result.Properties, p => p.Name == "Active");
    }

    [Fact]
    public async Task Preview_ShowsControlGroups()
    {
        await _entityService.ScaffoldAsync(_tempDir, "Deal", "Mod",
            properties: "Name:string,Status:enum(New|Active)");

        var result = await _service.PreviewAsync(Path.Combine(_tempDir, "Deal.mtd"));

        Assert.True(result.Success);
        Assert.NotEmpty(result.ControlGroups);
        Assert.Contains(result.ControlGroups, g => g.Name == "Main");
    }

    [Fact]
    public async Task Preview_ShowsLabelsFromResx()
    {
        await _entityService.ScaffoldAsync(_tempDir, "Deal", "Mod",
            properties: "Name:string", russianName: "Сделка");

        var result = await _service.PreviewAsync(Path.Combine(_tempDir, "Deal.mtd"));

        Assert.True(result.Success);
        Assert.Equal("Сделка", result.DisplayName);
    }

    [Fact]
    public async Task Preview_DirectoryInput_FindsMtd()
    {
        await _entityService.ScaffoldAsync(_tempDir, "MyEntity", "Mod");

        var result = await _service.PreviewAsync(_tempDir);

        Assert.True(result.Success);
        Assert.Equal("MyEntity", result.EntityName);
    }

    [Fact]
    public async Task Preview_ToMarkdown_ContainsTable()
    {
        await _entityService.ScaffoldAsync(_tempDir, "Deal", "Mod",
            properties: "Name:string,Amount:double");

        var result = await _service.PreviewAsync(Path.Combine(_tempDir, "Deal.mtd"));
        var md = result.ToMarkdown();

        Assert.Contains("Карточка:", md);
        Assert.Contains("Name", md);
        Assert.Contains("Amount", md);
        Assert.Contains("Свойства (2)", md);
    }

    [Fact]
    public async Task Preview_NonexistentPath_Fails()
    {
        var result = await _service.PreviewAsync("/nonexistent/path.mtd");
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Preview_EmptyPath_Fails()
    {
        var result = await _service.PreviewAsync("");
        Assert.False(result.Success);
    }
}
