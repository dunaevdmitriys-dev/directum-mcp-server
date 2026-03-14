using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class RefactorEntityToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly RefactorEntityTool _tool;

    // Default test entity MTD
    private const string EntityMtdJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
          "NameGuid": "test-entity-guid",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "ModuleName": "Test.Module",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata",
              "NameGuid": "prop-guid-1",
              "Name": "Title",
              "Code": "Title",
              "IsRequired": false
            },
            {
              "$type": "Sungero.Metadata.IntegerPropertyMetadata, Sungero.Metadata",
              "NameGuid": "prop-guid-2",
              "Name": "Amount",
              "Code": "Amount",
              "IsRequired": false
            }
          ]
        }
        """;

    // System.resx for tests
    private const string SystemResxXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="DisplayName" xml:space="preserve">
            <value>TestEntity</value>
          </data>
          <data name="Property_Title" xml:space="preserve">
            <value>Title</value>
          </data>
          <data name="Property_Amount" xml:space="preserve">
            <value>Amount</value>
          </data>
        </root>
        """;

    private const string ServerFunctionsCs = """
        using System;
        using Sungero.Core;

        namespace Test.Module.Server
        {
            partial class TestEntity
            {
                public virtual void DoSomethingWithTitle()
                {
                    var val = entity.Title;
                }
            }
        }
        """;

    public RefactorEntityToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "RefactorEntTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new RefactorEntityTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    /// <summary>Creates a standard entity directory with MTD, resx, and optional C# files.</summary>
    private string CreateEntityDir(string name, bool withCs = false)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "TestEntity.mtd"), EntityMtdJson);
        File.WriteAllText(Path.Combine(dir, "TestEntitySystem.resx"), SystemResxXml);
        File.WriteAllText(Path.Combine(dir, "TestEntitySystem.ru.resx"), SystemResxXml);

        if (withCs)
        {
            var serverDir = Path.Combine(dir, "Server");
            Directory.CreateDirectory(serverDir);
            File.WriteAllText(Path.Combine(serverDir, "TestEntityServerFunctions.cs"), ServerFunctionsCs);
        }

        return dir;
    }

    // ===================== rename_property =====================

    [Fact]
    public async Task RenameProperty_UpdatesMtd()
    {
        var dir = CreateEntityDir("rename_mtd");

        var result = await _tool.RefactorEntity(dir, "rename_property",
            propertyName: "Title", newName: "Caption", dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);
        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("Caption", mtd);
        Assert.DoesNotContain("\"Name\": \"Title\"", mtd);
    }

    [Fact]
    public async Task RenameProperty_UpdatesResx()
    {
        var dir = CreateEntityDir("rename_resx");

        await _tool.RefactorEntity(dir, "rename_property",
            propertyName: "Title", newName: "Caption", dryRun: false);

        var resx = File.ReadAllText(Path.Combine(dir, "TestEntitySystem.resx"));
        Assert.Contains("Property_Caption", resx);
        Assert.DoesNotContain("Property_Title", resx);
    }

    [Fact]
    public async Task RenameProperty_DryRun_NoFileChange()
    {
        var dir = CreateEntityDir("rename_dry");

        await _tool.RefactorEntity(dir, "rename_property",
            propertyName: "Title", newName: "Caption", dryRun: true);

        // Files should NOT be changed
        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("\"Name\": \"Title\"", mtd);

        var resx = File.ReadAllText(Path.Combine(dir, "TestEntitySystem.resx"));
        Assert.Contains("Property_Title", resx);
        Assert.DoesNotContain("Property_Caption", resx);
    }

    [Fact]
    public async Task RenameProperty_PropertyNotFound_ReturnsError()
    {
        var dir = CreateEntityDir("rename_notfound");

        var result = await _tool.RefactorEntity(dir, "rename_property",
            propertyName: "NonExistent", newName: "New", dryRun: false);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("NonExistent", result);
    }

    // ===================== add_property =====================

    [Fact]
    public async Task AddProperty_String_AddedToMtd()
    {
        var dir = CreateEntityDir("add_str");

        var result = await _tool.RefactorEntity(dir, "add_property",
            propertyName: "Description", propertyType: "string", dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);
        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("Description", mtd);
        Assert.Contains("StringPropertyMetadata", mtd);
    }

    [Fact]
    public async Task AddProperty_Enum_AddedWithValues()
    {
        var dir = CreateEntityDir("add_enum");

        var result = await _tool.RefactorEntity(dir, "add_property",
            propertyName: "Status", propertyType: "enum(Active|Closed|Draft)", dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);
        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("EnumPropertyMetadata", mtd);
        Assert.Contains("Active", mtd);
        Assert.Contains("Closed", mtd);
        Assert.Contains("Draft", mtd);
    }

    [Fact]
    public async Task AddProperty_AddsResxKey()
    {
        var dir = CreateEntityDir("add_resx");

        await _tool.RefactorEntity(dir, "add_property",
            propertyName: "Notes", propertyType: "text", dryRun: false);

        var resx = File.ReadAllText(Path.Combine(dir, "TestEntitySystem.resx"));
        Assert.Contains("Property_Notes", resx);
    }

    [Fact]
    public async Task AddProperty_DryRun_NoFileChange()
    {
        var dir = CreateEntityDir("add_dry");
        var mtdBefore = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));

        await _tool.RefactorEntity(dir, "add_property",
            propertyName: "NewProp", propertyType: "int", dryRun: true);

        var mtdAfter = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Equal(mtdBefore, mtdAfter);
        Assert.DoesNotContain("NewProp", mtdAfter);
    }

    // ===================== remove_property =====================

    [Fact]
    public async Task RemoveProperty_RemovesFromMtd()
    {
        var dir = CreateEntityDir("remove_mtd");

        var result = await _tool.RefactorEntity(dir, "remove_property",
            propertyName: "Amount", dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);
        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.DoesNotContain("\"Name\": \"Amount\"", mtd);
    }

    [Fact]
    public async Task RemoveProperty_RemovesResxKey()
    {
        var dir = CreateEntityDir("remove_resx");

        await _tool.RefactorEntity(dir, "remove_property",
            propertyName: "Title", dryRun: false);

        var resx = File.ReadAllText(Path.Combine(dir, "TestEntitySystem.resx"));
        Assert.DoesNotContain("Property_Title", resx);
    }

    [Fact]
    public async Task RemoveProperty_PropertyNotFound_ReturnsError()
    {
        var dir = CreateEntityDir("remove_notfound");

        var result = await _tool.RefactorEntity(dir, "remove_property",
            propertyName: "DoesNotExist", dryRun: false);

        Assert.Contains("ОШИБКА", result);
    }

    // ===================== change_base_type =====================

    [Fact]
    public async Task ChangeBaseType_UpdatesBaseGuid()
    {
        var dir = CreateEntityDir("chbase_guid");

        var result = await _tool.RefactorEntity(dir, "change_base_type",
            newBaseType: "Document", dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);
        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("58cca102-1e97-4f07-b6ac-fd866a8b7cb1", mtd); // Document GUID
    }

    [Fact]
    public async Task ChangeBaseType_UpdatesMetadataType()
    {
        var dir = CreateEntityDir("chbase_type");

        await _tool.RefactorEntity(dir, "change_base_type",
            newBaseType: "Task", dryRun: false);

        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("TaskMetadata", mtd);
    }

    [Fact]
    public async Task ChangeBaseType_InvalidType_ReturnsError()
    {
        var dir = CreateEntityDir("chbase_invalid");

        var result = await _tool.RefactorEntity(dir, "change_base_type",
            newBaseType: "UnknownType", dryRun: false);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("UnknownType", result);
    }

    // ===================== extract_to_databook =====================

    [Fact]
    public async Task ExtractToDatabook_CreatesNewEntity()
    {
        var dir = CreateEntityDir("extract_new");

        var result = await _tool.RefactorEntity(dir, "extract_to_databook",
            propertyName: "Title", dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);
        var parentDir = Path.GetDirectoryName(dir)!;
        var newEntityDir = Path.Combine(parentDir, "Title");
        Assert.True(Directory.Exists(newEntityDir));
        Assert.True(File.Exists(Path.Combine(newEntityDir, "Title.mtd")));
        Assert.True(File.Exists(Path.Combine(newEntityDir, "TitleSystem.resx")));
        Assert.True(File.Exists(Path.Combine(newEntityDir, "TitleSystem.ru.resx")));
    }

    [Fact]
    public async Task ExtractToDatabook_ReplacesWithNavigation()
    {
        var dir = CreateEntityDir("extract_nav");

        await _tool.RefactorEntity(dir, "extract_to_databook",
            propertyName: "Title", dryRun: false);

        var mtd = File.ReadAllText(Path.Combine(dir, "TestEntity.mtd"));
        Assert.Contains("NavigationPropertyMetadata", mtd);
        Assert.DoesNotContain("StringPropertyMetadata", mtd);
    }

    // ===================== security =====================

    [Fact]
    public async Task PathDenied_ReturnsDenyMessage()
    {
        // Use a path outside the SOLUTION_PATH (which points to _tempDir)
        var outsideDir = Path.Combine(
            Path.GetPathRoot(_tempDir) ?? "C:\\",
            "SomeOtherDirThatIsNotTemp_" + Guid.NewGuid().ToString("N")[..6]);

        var result = await _tool.RefactorEntity(outsideDir, "rename_property",
            propertyName: "Title", newName: "Caption");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }
}
