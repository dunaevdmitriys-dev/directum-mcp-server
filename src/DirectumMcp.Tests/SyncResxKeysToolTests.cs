using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class SyncResxKeysToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly SyncResxKeysTool _tool;

    private const string EntityMtd = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "11111111-1111-1111-1111-111111111111",
              "Name": "Title",
              "Code": "Title"
            },
            {
              "$type": "Sungero.Metadata.EnumPropertyMetadata",
              "NameGuid": "22222222-2222-2222-2222-222222222222",
              "Name": "Status",
              "Code": "Status",
              "DirectValues": [
                { "NameGuid": "33333333-3333-3333-3333-333333333333", "Name": "Active" },
                { "NameGuid": "44444444-4444-4444-4444-444444444444", "Name": "Closed" }
              ]
            }
          ],
          "Actions": [
            {
              "NameGuid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "Name": "Approve",
              "IsAncestorMetadata": false
            }
          ]
        }
        """;

    private const string EmptyResx = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <resheader name="version"><value>2.0</value></resheader>
        </root>
        """;

    private const string PartialResx = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <resheader name="version"><value>2.0</value></resheader>
          <data name="Property_Title" xml:space="preserve">
            <value>Заголовок</value>
          </data>
        </root>
        """;

    public SyncResxKeysToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SyncResxTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new SyncResxKeysTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreatePackage(string name)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public async Task Sync_DryRun_ReportsMissingKeys()
    {
        var pkg = CreatePackage("pkg_dry");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        File.WriteAllText(Path.Combine(pkg, "TestEntitySystem.resx"), EmptyResx);

        var result = await _tool.SyncResxKeys(pkg, dryRun: true);

        Assert.Contains("Property_Title", result);
        Assert.Contains("Property_Status", result);
        Assert.Contains("Action_Approve", result);
        Assert.Contains("DisplayName", result);
    }

    [Fact]
    public async Task Sync_DryRun_ReportsEnumKeys()
    {
        var pkg = CreatePackage("pkg_enum");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        File.WriteAllText(Path.Combine(pkg, "TestEntitySystem.resx"), EmptyResx);

        var result = await _tool.SyncResxKeys(pkg, dryRun: true);

        Assert.Contains("Enum_Status_Active", result);
        Assert.Contains("Enum_Status_Closed", result);
    }

    [Fact]
    public async Task Sync_DryRun_DoesNotModifyFiles()
    {
        var pkg = CreatePackage("pkg_nomod");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        var resxPath = Path.Combine(pkg, "TestEntitySystem.resx");
        File.WriteAllText(resxPath, EmptyResx);
        var originalContent = File.ReadAllText(resxPath);

        await _tool.SyncResxKeys(pkg, dryRun: true);

        Assert.Equal(originalContent, File.ReadAllText(resxPath));
    }

    [Fact]
    public async Task Sync_Apply_AddsKeysToResx()
    {
        var pkg = CreatePackage("pkg_apply");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        var resxPath = Path.Combine(pkg, "TestEntitySystem.resx");
        File.WriteAllText(resxPath, EmptyResx);

        await _tool.SyncResxKeys(pkg, dryRun: false);

        var content = File.ReadAllText(resxPath);
        Assert.Contains("Property_Title", content);
        Assert.Contains("Property_Status", content);
        Assert.Contains("Action_Approve", content);
        Assert.Contains("DisplayName", content);
    }

    [Fact]
    public async Task Sync_PartialResx_OnlyAddsMissing()
    {
        var pkg = CreatePackage("pkg_partial");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        var resxPath = Path.Combine(pkg, "TestEntitySystem.resx");
        File.WriteAllText(resxPath, PartialResx);

        var result = await _tool.SyncResxKeys(pkg, dryRun: true);

        // Property_Title already exists — should not be in missing list
        // But Property_Status, Action_Approve should be
        Assert.Contains("Property_Status", result);
        Assert.Contains("Action_Approve", result);
    }

    [Fact]
    public async Task Sync_SkipsInheritedProperties()
    {
        var pkg = CreatePackage("pkg_inherited");
        var mtd = """
            {
              "$type": "Sungero.Metadata.EntityMetadata",
              "NameGuid": "a1b2c3d4-0000-0000-0000-000000000000",
              "Name": "ChildEntity",
              "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
              "Properties": [
                {
                  "$type": "Sungero.Metadata.StringPropertyMetadata",
                  "NameGuid": "55555555-5555-5555-5555-555555555555",
                  "Name": "InheritedProp",
                  "IsAncestorMetadata": true
                },
                {
                  "$type": "Sungero.Metadata.StringPropertyMetadata",
                  "NameGuid": "66666666-6666-6666-6666-666666666666",
                  "Name": "OwnProp"
                }
              ],
              "Actions": []
            }
            """;
        File.WriteAllText(Path.Combine(pkg, "ChildEntity.mtd"), mtd);
        File.WriteAllText(Path.Combine(pkg, "ChildEntitySystem.resx"), EmptyResx);

        var result = await _tool.SyncResxKeys(pkg, dryRun: true);

        Assert.Contains("Property_OwnProp", result);
        Assert.DoesNotContain("Property_InheritedProp", result);
    }

    [Fact]
    public async Task Sync_NoResxFile_ReportsWarning()
    {
        var pkg = CreatePackage("pkg_noresx");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        // No resx file at all

        var result = await _tool.SyncResxKeys(pkg, dryRun: true);

        // Should still work — report that resx not found
        Assert.Contains("TestEntity", result);
    }

    [Fact]
    public async Task Sync_NonexistentPath_ReturnsError()
    {
        var result = await _tool.SyncResxKeys(Path.Combine(_tempDir, "no_such"));

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Sync_ReportContainsSummary()
    {
        var pkg = CreatePackage("pkg_summary");
        File.WriteAllText(Path.Combine(pkg, "TestEntity.mtd"), EntityMtd);
        File.WriteAllText(Path.Combine(pkg, "TestEntitySystem.resx"), EmptyResx);

        var result = await _tool.SyncResxKeys(pkg, dryRun: true);

        Assert.Contains("Итого", result);
    }
}
