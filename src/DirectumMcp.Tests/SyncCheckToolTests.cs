using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class SyncCheckToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly SyncCheckTool _tool;

    // MTD with two properties: Title (string) and Status (enum with Draft, Active, Closed)
    private const string EntityMtdFull = """
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
                { "NameGuid": "d1000000-0000-0000-0000-000000000001", "Name": "Draft" },
                { "NameGuid": "d1000000-0000-0000-0000-000000000002", "Name": "Active" },
                { "NameGuid": "d1000000-0000-0000-0000-000000000003", "Name": "Closed" }
              ]
            }
          ],
          "Actions": []
        }
        """;

    // MTD with only Title (no Status, no NewField)
    private const string EntityMtdSimple = """
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
            }
          ],
          "Actions": []
        }
        """;

    // MTD with Title + NewField (added in source)
    private const string EntityMtdWithNewField = """
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
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "33333333-3333-3333-3333-333333333333",
              "Name": "NewField",
              "Code": "NewField"
            }
          ],
          "Actions": []
        }
        """;

    // MTD with Status enum having only Draft, Active (Closed removed)
    private const string EntityMtdEnumReduced = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.EnumPropertyMetadata",
              "NameGuid": "22222222-2222-2222-2222-222222222222",
              "Name": "Status",
              "Code": "Status",
              "DirectValues": [
                { "NameGuid": "d1000000-0000-0000-0000-000000000001", "Name": "Draft" },
                { "NameGuid": "d1000000-0000-0000-0000-000000000002", "Name": "Active" }
              ]
            }
          ],
          "Actions": []
        }
        """;

    private const string ResxWithTwoKeys = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="Property_Title" xml:space="preserve">
            <value>Заголовок</value>
          </data>
          <data name="Property_Description" xml:space="preserve">
            <value>Описание</value>
          </data>
        </root>
        """;

    private const string ResxWithOneKey = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="Property_Title" xml:space="preserve">
            <value>Заголовок</value>
          </data>
        </root>
        """;

    public SyncCheckToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "SyncCheckTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new SyncCheckTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private string CreateDir(string name)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFile(string dir, string fileName, string content)
    {
        var filePath = Path.Combine(dir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
    }

    // =========================================================================
    // Test 1: Identical directories → "Совпадает"
    // =========================================================================

    [Fact]
    public async Task SyncCheck_IdenticalDirectories_ShowsMatchStatus()
    {
        var source = CreateDir("t1_source");
        var published = CreateDir("t1_pub");
        WriteFile(source, "Entity.mtd", EntityMtdSimple);
        WriteFile(published, "Entity.mtd", EntityMtdSimple);

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("Совпадает", result);
        Assert.Contains("Совпадают: 1", result);
        Assert.Contains("Различаются: 0", result);
    }

    // =========================================================================
    // Test 2: File only in source → "Только в исходниках"
    // =========================================================================

    [Fact]
    public async Task SyncCheck_FileOnlyInSource_ShowsOnlyInSource()
    {
        var source = CreateDir("t2_source");
        var published = CreateDir("t2_pub");
        WriteFile(source, "NewEntity.mtd", EntityMtdSimple);
        // published is empty

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("Только в исходниках", result);
        Assert.Contains("NewEntity.mtd", result);
        Assert.Contains("Только в исходниках: 1", result);
    }

    // =========================================================================
    // Test 3: File only in published → "Только на стенде"
    // =========================================================================

    [Fact]
    public async Task SyncCheck_FileOnlyInPublished_ShowsOnlyOnStand()
    {
        var source = CreateDir("t3_source");
        var published = CreateDir("t3_pub");
        // source is empty
        WriteFile(published, "OldEntity.mtd", EntityMtdSimple);

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("Только на стенде", result);
        Assert.Contains("OldEntity.mtd", result);
        Assert.Contains("Только на стенде: 1", result);
    }

    // =========================================================================
    // Test 4: Property differences — added in source, removed from published
    // =========================================================================

    [Fact]
    public async Task SyncCheck_PropertyDifferences_ShowsAddedAndRemoved()
    {
        var source = CreateDir("t4_source");
        var published = CreateDir("t4_pub");
        // source has Title + NewField; published has Title + Status
        WriteFile(source, "Entity.mtd", EntityMtdWithNewField);
        WriteFile(published, "Entity.mtd", EntityMtdFull);

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("Различается", result);
        // NewField is in source but not published → added
        Assert.Contains("NewField", result);
        Assert.Contains("Добавленные свойства", result);
        // Status is in published but not source → removed
        Assert.Contains("Status", result);
        Assert.Contains("Удалённые свойства", result);
    }

    // =========================================================================
    // Test 5: Enum value differences
    // =========================================================================

    [Fact]
    public async Task SyncCheck_EnumDifferences_ShowsChangedValues()
    {
        var source = CreateDir("t5_source");
        var published = CreateDir("t5_pub");
        // source has Status: Draft, Active, Closed
        // published has Status: Draft, Active (Closed removed)
        WriteFile(source, "Entity.mtd", EntityMtdFull);
        WriteFile(published, "Entity.mtd", EntityMtdEnumReduced);

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("Изменённые Enum-значения", result);
        Assert.Contains("Status", result);
        Assert.Contains("Draft", result);
        Assert.Contains("Active", result);
        Assert.Contains("Closed", result);
    }

    // =========================================================================
    // Test 6: Resx key differences — shows new/deleted counts
    // =========================================================================

    [Fact]
    public async Task SyncCheck_ResxKeyDifferences_ShowsNewAndDeleted()
    {
        var source = CreateDir("t6_source");
        var published = CreateDir("t6_pub");
        // source has 2 resx keys; published has 1
        WriteFile(source, "EntitySystem.resx", ResxWithTwoKeys);
        WriteFile(published, "EntitySystem.resx", ResxWithOneKey);

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("Resx", result);
        Assert.Contains("EntitySystem.resx", result);
        // Property_Description is new in source (not in published)
        Assert.Contains("1", result); // at least 1 new key
    }

    // =========================================================================
    // Test 7: Nonexistent source path → error
    // =========================================================================

    [Fact]
    public async Task SyncCheck_NonexistentSourcePath_ReturnsError()
    {
        var published = CreateDir("t7_pub");
        var noSuchPath = Path.Combine(_tempDir, "no_such_directory");

        var result = await _tool.SyncCheck(noSuchPath, published);

        Assert.Contains("ОШИБКА", result);
    }

    // =========================================================================
    // Test 8: Path outside SOLUTION_PATH → access denied
    // =========================================================================

    [Fact]
    public async Task SyncCheck_PathOutsideSolutionPath_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr";
        if (!Directory.Exists(outsidePath))
            return; // Skip on environments without accessible outside dir

        var published = CreateDir("t8_pub");

        var result = await _tool.SyncCheck(outsidePath, published);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    // =========================================================================
    // Extra Test 9: Summary section is present
    // =========================================================================

    [Fact]
    public async Task SyncCheck_AlwaysContainsSummarySection()
    {
        var source = CreateDir("t9_source");
        var published = CreateDir("t9_pub");
        WriteFile(source, "Entity.mtd", EntityMtdSimple);
        WriteFile(published, "Entity.mtd", EntityMtdSimple);

        var result = await _tool.SyncCheck(source, published);

        Assert.Contains("## Итого", result);
        Assert.Contains("Совпадают:", result);
        Assert.Contains("Различаются:", result);
        Assert.Contains("Только в исходниках:", result);
        Assert.Contains("Только на стенде:", result);
    }

    // =========================================================================
    // Extra Test 10: Nonexistent published path → error
    // =========================================================================

    [Fact]
    public async Task SyncCheck_NonexistentPublishedPath_ReturnsError()
    {
        var source = CreateDir("t10_source");
        var noSuchPath = Path.Combine(_tempDir, "no_such_published");

        var result = await _tool.SyncCheck(source, noSuchPath);

        Assert.Contains("ОШИБКА", result);
    }
}
