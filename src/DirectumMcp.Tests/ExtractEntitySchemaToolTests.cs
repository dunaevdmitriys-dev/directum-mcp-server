using System.Text.Json;
using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ExtractEntitySchemaToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly ExtractEntitySchemaTool _tool;

    // Full entity MTD: DatabookEntry, enum with DirectValues, nav property, collection,
    // one own action, one inherited action, attachment group with constraints.
    private const string FullEntityMtdJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "Contract",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "IsAbstract": false,
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "11111111-1111-1111-1111-111111111111",
              "Name": "Subject",
              "Code": "Subject",
              "IsRequired": true,
              "IsAncestorMetadata": false
            },
            {
              "$type": "Sungero.Metadata.EnumPropertyMetadata",
              "NameGuid": "22222222-2222-2222-2222-222222222222",
              "Name": "Status",
              "Code": "Status",
              "IsRequired": false,
              "IsAncestorMetadata": false,
              "DirectValues": [
                { "NameGuid": "33333333-3333-3333-3333-333333333333", "Name": "Draft", "Code": "Draft" },
                { "NameGuid": "44444444-4444-4444-4444-444444444444", "Name": "Active", "Code": "Active" },
                { "NameGuid": "55555555-5555-5555-5555-555555555555", "Name": "Closed", "Code": "Closed" }
              ]
            },
            {
              "$type": "Sungero.Metadata.NavigationPropertyMetadata",
              "NameGuid": "66666666-6666-6666-6666-666666666666",
              "Name": "Counterparty",
              "Code": "Counterparty",
              "IsRequired": false,
              "IsAncestorMetadata": false,
              "EntityGuid": "99999999-9999-9999-9999-999999999999"
            },
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "77777777-7777-7777-7777-777777777777",
              "Name": "Name",
              "Code": "Name",
              "IsRequired": true,
              "IsAncestorMetadata": true
            },
            {
              "$type": "Sungero.Metadata.CollectionPropertyMetadata",
              "NameGuid": "88888888-8888-8888-8888-888888888888",
              "Name": "Lines",
              "Code": "Lines",
              "IsAncestorMetadata": false,
              "EntityGuid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
            }
          ],
          "Actions": [
            {
              "NameGuid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
              "Name": "Approve",
              "IsAncestorMetadata": false
            },
            {
              "NameGuid": "cccccccc-cccc-cccc-cccc-cccccccccccc",
              "Name": "Save",
              "IsAncestorMetadata": true
            }
          ],
          "AttachmentGroups": [
            {
              "NameGuid": "dddddddd-dddd-dddd-dddd-dddddddddddd",
              "Name": "AddendumGroup",
              "IsAssociatedEntityGroup": false,
              "Constraints": [
                { "Name": "OnlyDocuments", "EntityGuid": "eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee" }
              ]
            }
          ]
        }
        """;

    // Minimal entity: no properties, no actions, no collections, no attachment groups.
    private const string EmptyEntityMtdJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "00000000-0000-0000-0000-000000000001",
          "Name": "EmptyEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0"
        }
        """;

    // Task entity with inherited property.
    private const string TaskEntityMtdJson = """
        {
          "$type": "Sungero.Metadata.TaskMetadata",
          "NameGuid": "00000000-0000-0000-0000-000000000002",
          "Name": "MyTask",
          "BaseGuid": "d795d1f6-45c1-4e5e-9677-b53fb7280c7e",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "00000000-0000-0000-0000-000000000010",
              "Name": "Subject",
              "Code": "Subject",
              "IsRequired": true,
              "IsAncestorMetadata": false
            },
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "00000000-0000-0000-0000-000000000011",
              "Name": "InheritedProp",
              "Code": "InheritedProp",
              "IsRequired": false,
              "IsAncestorMetadata": true
            }
          ],
          "Actions": [],
          "AttachmentGroups": []
        }
        """;

    // Module MTD — must be rejected by extract_entity_schema.
    private const string ModuleMtdJson = """
        {
          "$type": "Sungero.Metadata.ModuleMetadata",
          "NameGuid": "00000000-0000-0000-0000-000000000003",
          "Name": "TestModule",
          "Version": "1.0.0.0"
        }
        """;

    public ExtractEntitySchemaToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ExtractSchemaTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new ExtractEntitySchemaTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Helpers

    private string CreateFile(string fileName, string content)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    #endregion

    #region Markdown format

    [Fact]
    public async Task ExtractEntitySchema_MarkdownFormat_ContainsEntityHeader()
    {
        // Arrange
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        // Act
        var result = await _tool.ExtractEntitySchema(path, "markdown");

        // Assert: entity name and kind present
        Assert.Contains("Contract", result);
        Assert.Contains("DatabookEntry", result);
        Assert.Contains("Схема сущности", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_MarkdownFormat_ContainsPropertiesTable()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("Subject", result);
        Assert.Contains("String", result);
        // Required field indicator
        Assert.Contains("да", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_MarkdownFormat_ContainsActionsTable()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("Действия", result);
        Assert.Contains("Approve", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_MarkdownFormat_ContainsAttachmentGroups()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("Группы вложений", result);
        Assert.Contains("AddendumGroup", result);
        Assert.Contains("OnlyDocuments", result);
    }

    #endregion

    #region JSON Schema format

    [Fact]
    public async Task ExtractEntitySchema_JsonSchemaFormat_IsValidJson()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "json-schema");

        // Must be parseable JSON
        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.Equal("http://json-schema.org/draft-07/schema#", root.GetProperty("$schema").GetString());
        Assert.Equal("Contract", root.GetProperty("title").GetString());
        Assert.Equal("object", root.GetProperty("type").GetString());
    }

    [Fact]
    public async Task ExtractEntitySchema_JsonSchemaFormat_ContainsRequiredArray()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "json-schema");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("required", out var req));
        var requiredNames = req.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Subject", requiredNames);
    }

    [Fact]
    public async Task ExtractEntitySchema_JsonSchemaFormat_EnumHasEnumValues()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "json-schema");

        using var doc = JsonDocument.Parse(result);
        var props = doc.RootElement.GetProperty("properties");
        Assert.True(props.TryGetProperty("Status", out var statusProp));
        Assert.True(statusProp.TryGetProperty("enum", out var enumArr));

        var enumVals = enumArr.EnumerateArray().Select(e => e.GetString()).ToList();
        Assert.Contains("Draft", enumVals);
        Assert.Contains("Active", enumVals);
        Assert.Contains("Closed", enumVals);
    }

    [Fact]
    public async Task ExtractEntitySchema_JsonSchemaFormat_NavigationPropertyHasDescription()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "json-schema");

        using var doc = JsonDocument.Parse(result);
        var props = doc.RootElement.GetProperty("properties");
        Assert.True(props.TryGetProperty("Counterparty", out var navProp));
        Assert.Equal("object", navProp.GetProperty("type").GetString());
        // Description must reference the EntityGuid
        Assert.Contains("99999999-9999-9999-9999-999999999999",
            navProp.GetProperty("description").GetString() ?? "");
    }

    #endregion

    #region include_inherited flag

    [Fact]
    public async Task ExtractEntitySchema_IncludeInheritedFalse_ExcludesInheritedProperties()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown", includeInherited: false);

        // "Name" is IsAncestorMetadata=true — must NOT appear in the properties table
        // But it should also not appear in the table at all (it still appears in the header "Name" column header)
        // We check that the "унаследовано" marker is NOT in the output
        Assert.DoesNotContain("унаследовано", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_IncludeInheritedTrue_IncludesInheritedProperties()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown", includeInherited: true);

        // With include_inherited=true, "Name" (IsAncestorMetadata=true) must appear with "унаследовано" marker
        Assert.Contains("унаследовано", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_IncludeInheritedTrue_TaskIncludesInheritedAction()
    {
        // TaskEntityMtdJson has inherited action "Save" (IsAncestorMetadata=true)
        // But FullEntityMtdJson has inherited action "Save" too — use that
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var resultWithout = await _tool.ExtractEntitySchema(path, "markdown", includeInherited: false);
        var resultWith = await _tool.ExtractEntitySchema(path, "markdown", includeInherited: true);

        // "Save" action is inherited — should appear only when include_inherited=true
        Assert.DoesNotContain("Save", resultWithout);
        Assert.Contains("Save", resultWith);
    }

    #endregion

    #region Enum with DirectValues

    [Fact]
    public async Task ExtractEntitySchema_EnumProperty_ShowsDirectValues()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("Draft", result);
        Assert.Contains("Active", result);
        Assert.Contains("Closed", result);
    }

    #endregion

    #region NavigationProperty

    [Fact]
    public async Task ExtractEntitySchema_NavigationProperty_ShowsTargetGuid()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("99999999-9999-9999-9999-999999999999", result);
        Assert.Contains("Navigation", result);
    }

    #endregion

    #region Nonexistent file

    [Fact]
    public async Task ExtractEntitySchema_NonexistentFile_ReturnsError()
    {
        var path = Path.Combine(_tempDir, "does_not_exist.mtd");

        var result = await _tool.ExtractEntitySchema(path);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найден", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_PathOutsideAllowed_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr";

        if (!Directory.Exists(outsidePath))
            return; // skip if neither path available

        var fakeMtd = Path.Combine(outsidePath, "fake.mtd");

        var result = await _tool.ExtractEntitySchema(fakeMtd);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion

    #region Empty properties

    [Fact]
    public async Task ExtractEntitySchema_EmptyProperties_ReturnsNoPropertiesMessage()
    {
        var path = CreateFile("EmptyEntity.mtd", EmptyEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("EmptyEntity", result);
        // The section header "Свойства" must still appear (with the absence note)
        Assert.Contains("Свойства", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_EmptyProperties_JsonSchemaHasNoRequired()
    {
        var path = CreateFile("EmptyEntity.mtd", EmptyEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "json-schema");

        using var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        // No required array when there are no required properties
        Assert.False(root.TryGetProperty("required", out _));
    }

    #endregion

    #region Module MTD rejection

    [Fact]
    public async Task ExtractEntitySchema_ModuleMtd_ReturnsError()
    {
        var path = CreateFile("Module.mtd", ModuleMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("метаданными", result);
    }

    #endregion

    #region Unknown format

    [Fact]
    public async Task ExtractEntitySchema_UnknownFormat_ReturnsError()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "xml");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("xml", result);
    }

    #endregion

    #region Collections

    [Fact]
    public async Task ExtractEntitySchema_Collections_AppearsInMarkdown()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "markdown");

        Assert.Contains("Коллекции", result);
        Assert.Contains("Lines", result);
    }

    [Fact]
    public async Task ExtractEntitySchema_Collections_AppearsInJsonSchema()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        var result = await _tool.ExtractEntitySchema(path, "json-schema");

        using var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("collections", out var cols));
        Assert.True(cols.TryGetProperty("Lines", out _));
    }

    #endregion

    #region Default format

    [Fact]
    public async Task ExtractEntitySchema_DefaultFormat_IsMarkdown()
    {
        var path = CreateFile("Contract.mtd", FullEntityMtdJson);

        // Call without specifying format
        var result = await _tool.ExtractEntitySchema(path);

        // Markdown output has ## headers and | table separators
        Assert.Contains("##", result);
        Assert.Contains("|", result);
    }

    #endregion
}
