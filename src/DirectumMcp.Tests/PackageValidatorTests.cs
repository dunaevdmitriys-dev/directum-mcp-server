using System.Text.Json;
using DirectumMcp.Core.Validators;
using Xunit;

namespace DirectumMcp.Tests;

public class PackageValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public PackageValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DirectumMcpPkgTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Helpers

    private const string DatabookEntryBaseGuid = "04581d26-0780-4cfd-b3cd-c2cafc5798b0";
    private const string DocumentBaseGuid = "58cca102-1e97-4f07-b6ac-fd866a8b7cb1";

    private static List<(string Path, JsonDocument Doc)> ParseEntities(params string[] jsons)
    {
        return jsons
            .Select((json, i) => ($"entity{i}.mtd", JsonDocument.Parse(json)))
            .ToList();
    }

    private static string BuildResxXml(Dictionary<string, string> entries)
    {
        var dataElements = string.Join(Environment.NewLine,
            entries.Select(kv =>
                $"  <data name=\"{kv.Key}\" xml:space=\"preserve\">\n    <value>{kv.Value}</value>\n  </data>"));

        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <resheader name="resmimetype">
                <value>text/microsoft-resx</value>
              </resheader>
              <resheader name="version">
                <value>2.0</value>
              </resheader>
            {dataElements}
            </root>
            """;
    }

    private string CreateResxFile(string fileName, Dictionary<string, string> entries)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        File.WriteAllText(filePath, BuildResxXml(entries));
        return filePath;
    }

    private static string DatabookEntityWithCollection(string nameGuid = "aaaaaaaa-0000-0000-0000-000000000001") => $$"""
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "{{nameGuid}}",
          "Name": "MyDatabookEntity",
          "BaseGuid": "{{DatabookEntryBaseGuid}}",
          "Properties": [
            {
              "$type": "Sungero.Metadata.CollectionPropertyMetadata",
              "NameGuid": "cccccccc-0000-0000-0000-000000000001",
              "Name": "Items",
              "Code": "Items"
            }
          ]
        }
        """;

    private static string DatabookEntityNoCollection(string nameGuid = "aaaaaaaa-0000-0000-0000-000000000002") => $$"""
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "{{nameGuid}}",
          "Name": "CleanDatabookEntity",
          "BaseGuid": "{{DatabookEntryBaseGuid}}",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "dddddddd-0000-0000-0000-000000000001",
              "Name": "Name",
              "Code": "Name"
            }
          ]
        }
        """;

    private static string DocumentEntityWithCollection(string nameGuid = "aaaaaaaa-0000-0000-0000-000000000003") => $$"""
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "{{nameGuid}}",
          "Name": "MyDocument",
          "BaseGuid": "{{DocumentBaseGuid}}",
          "Properties": [
            {
              "$type": "Sungero.Metadata.CollectionPropertyMetadata",
              "NameGuid": "eeeeeeee-0000-0000-0000-000000000001",
              "Name": "Versions",
              "Code": "Versions"
            }
          ]
        }
        """;

    private static string EntityWithEnumValue(string valueName, string nameGuid = "bbbbbbbb-0000-0000-0000-000000000001") => $$"""
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "{{nameGuid}}",
          "Name": "StatusEntity",
          "BaseGuid": "{{DatabookEntryBaseGuid}}",
          "Properties": [
            {
              "$type": "Sungero.Metadata.EnumPropertyMetadata",
              "NameGuid": "ffffffff-0000-0000-0000-000000000001",
              "Name": "Status",
              "Code": "Status",
              "DirectValues": [
                { "Name": "{{valueName}}", "NameGuid": "11111111-0000-0000-0000-000000000001" }
              ]
            }
          ]
        }
        """;

    private static string EntityWithMultipleEnumValues(string[] valueNames, string nameGuid = "bbbbbbbb-0000-0000-0000-000000000002")
    {
        var valuesJson = string.Join(",\n", valueNames.Select((v, i) =>
            $"{{ \"Name\": \"{v}\", \"NameGuid\": \"{i:D8}-0000-0000-0000-000000000001\" }}"));
        return $$"""
            {
              "$type": "Sungero.Metadata.EntityMetadata",
              "NameGuid": "{{nameGuid}}",
              "Name": "MultiEnumEntity",
              "BaseGuid": "{{DatabookEntryBaseGuid}}",
              "Properties": [
                {
                  "$type": "Sungero.Metadata.EnumPropertyMetadata",
                  "NameGuid": "ffffffff-0000-0000-0000-000000000002",
                  "Name": "Category",
                  "Code": "Category",
                  "DirectValues": [
                    {{valuesJson}}
                  ]
                }
              ]
            }
            """;
    }

    private static string EntityWithCodeProperty(string baseGuid, string entityNameGuid, string entityName, string propertyCode) => $$"""
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "{{entityNameGuid}}",
          "Name": "{{entityName}}",
          "BaseGuid": "{{baseGuid}}",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "12345678-0000-0000-0000-000000000001",
              "Name": "{{propertyCode}}",
              "Code": "{{propertyCode}}"
            }
          ]
        }
        """;

    #endregion

    #region Check1: CollectionPropertyMetadata on DatabookEntry

    [Fact]
    public void Check1_DatabookEntryWithCollection_ReturnsError()
    {
        // Arrange
        var entities = ParseEntities(DatabookEntityWithCollection());

        // Act
        var results = PackageValidator.Check1_CollectionOnDatabookEntry(entities).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Type);
        Assert.Equal("Check1_CollectionOnDatabookEntry", results[0].CheckName);
        Assert.False(results[0].CanAutoFix);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check1_DatabookEntryWithoutCollection_ReturnsEmpty()
    {
        // Arrange
        var entities = ParseEntities(DatabookEntityNoCollection());

        // Act
        var results = PackageValidator.Check1_CollectionOnDatabookEntry(entities).ToList();

        // Assert
        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check1_DocumentEntityWithCollection_ReturnsEmpty()
    {
        // Arrange — Document with collection is allowed
        var entities = ParseEntities(DocumentEntityWithCollection());

        // Act
        var results = PackageValidator.Check1_CollectionOnDatabookEntry(entities).ToList();

        // Assert
        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check1_EmptyEntityList_ReturnsEmpty()
    {
        var results = PackageValidator.Check1_CollectionOnDatabookEntry(new List<(string, JsonDocument)>()).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void Check1_MultipleEntities_OnlyFlagsViolatingOnes()
    {
        // Arrange: one violating, one clean, one document (all different scenarios)
        var entities = ParseEntities(
            DatabookEntityWithCollection("aaaaaaaa-0000-0000-0000-000000000001"),
            DatabookEntityNoCollection("aaaaaaaa-0000-0000-0000-000000000002"),
            DocumentEntityWithCollection("aaaaaaaa-0000-0000-0000-000000000003"));

        // Act
        var results = PackageValidator.Check1_CollectionOnDatabookEntry(entities).ToList();

        // Assert: only the DatabookEntry with collection is flagged
        Assert.Single(results);
        Assert.Contains("MyDatabookEntity", results[0].Message);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region Check3: Reserved C# words in enum values

    [Fact]
    public void Check3_EnumValueNamedNew_ReturnsError()
    {
        // Arrange
        var entities = ParseEntities(EntityWithEnumValue("new"));

        // Act
        var results = PackageValidator.Check3_ReservedEnumNames(entities).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Type);
        Assert.Equal("Check3_ReservedEnumValues", results[0].CheckName);
        Assert.Contains("new", results[0].Message);
        Assert.True(results[0].CanAutoFix);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check3_EnumValueNamedMyCustomValue_ReturnsEmpty()
    {
        // Arrange
        var entities = ParseEntities(EntityWithEnumValue("MyCustomValue"));

        // Act
        var results = PackageValidator.Check3_ReservedEnumNames(entities).ToList();

        // Assert
        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check3_MultipleReservedWords_ReturnsMultipleErrors()
    {
        // Arrange: enum has "new", "class", and a safe value
        var entities = ParseEntities(EntityWithMultipleEnumValues(new[] { "new", "class", "MyValidValue" }));

        // Act
        var results = PackageValidator.Check3_ReservedEnumNames(entities).ToList();

        // Assert: two reserved words produce two errors
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Error, r.Type));
        Assert.Contains(results, r => r.Message.Contains("new"));
        Assert.Contains(results, r => r.Message.Contains("class"));

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check3_AllKnownReservedWords_AllFlagged()
    {
        // Spot-check a handful of reserved words beyond "new"
        var reserved = new[] { "event", "return", "void", "string", "int", "bool" };
        foreach (var word in reserved)
        {
            var entities = ParseEntities(EntityWithEnumValue(word));
            var results = PackageValidator.Check3_ReservedEnumNames(entities).ToList();
            Assert.Single(results);
            Assert.Contains(word, results[0].Message);
            foreach (var (_, doc) in entities) doc.Dispose();
        }
    }

    [Fact]
    public void Check3_EntityWithNoEnums_ReturnsEmpty()
    {
        var entities = ParseEntities(DatabookEntityNoCollection());

        var results = PackageValidator.Check3_ReservedEnumNames(entities).ToList();

        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region Check4: Duplicate DB column Codes

    [Fact]
    public void Check4_TwoEntitiesSameBaseGuidSameCode_ReturnsError()
    {
        // Arrange: two sibling entities sharing the same BaseGuid and same property Code
        const string sharedBaseGuid = "99999999-0000-0000-0000-000000000000";
        var entity1 = EntityWithCodeProperty(sharedBaseGuid, "11111111-0000-0000-0000-000000000001", "EntityA", "Deal");
        var entity2 = EntityWithCodeProperty(sharedBaseGuid, "22222222-0000-0000-0000-000000000001", "EntityB", "Deal");

        var entities = ParseEntities(entity1, entity2);

        // Act
        var results = PackageValidator.Check4_DuplicateCodes(entities).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Type);
        Assert.Equal("Check4_DuplicateCodes", results[0].CheckName);
        Assert.Contains("Deal", results[0].Message);
        Assert.True(results[0].CanAutoFix);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check4_TwoEntitiesSameBaseGuidDifferentCodes_ReturnsEmpty()
    {
        // Arrange
        const string sharedBaseGuid = "99999999-0000-0000-0000-000000000000";
        var entity1 = EntityWithCodeProperty(sharedBaseGuid, "11111111-0000-0000-0000-000000000001", "EntityA", "CPDeal");
        var entity2 = EntityWithCodeProperty(sharedBaseGuid, "22222222-0000-0000-0000-000000000001", "EntityB", "InvDeal");

        var entities = ParseEntities(entity1, entity2);

        // Act
        var results = PackageValidator.Check4_DuplicateCodes(entities).ToList();

        // Assert
        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check4_TwoEntitiesDifferentBaseGuidSameCode_ReturnsEmpty()
    {
        // Arrange: same property Code but different hierarchies (different BaseGuid) — no conflict
        var entity1 = EntityWithCodeProperty(
            "99999999-0000-0000-0000-000000000001",
            "11111111-0000-0000-0000-000000000001", "EntityA", "Deal");
        var entity2 = EntityWithCodeProperty(
            "99999999-0000-0000-0000-000000000002",
            "22222222-0000-0000-0000-000000000001", "EntityB", "Deal");

        var entities = ParseEntities(entity1, entity2);

        // Act
        var results = PackageValidator.Check4_DuplicateCodes(entities).ToList();

        // Assert
        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check4_SingleEntityWithCode_ReturnsEmpty()
    {
        var entity = EntityWithCodeProperty(
            DatabookEntryBaseGuid,
            "11111111-0000-0000-0000-000000000001", "OnlyEntity", "SomeCode");

        var entities = ParseEntities(entity);
        var results = PackageValidator.Check4_DuplicateCodes(entities).ToList();

        Assert.Empty(results);

        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region Check6: System.resx key format

    [Fact]
    public async Task Check6_ResourceGuidKey_ReturnsErrorWithCanAutoFix()
    {
        // Arrange
        var filePath = CreateResxFile("EntitySystem.resx", new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Название"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { filePath })).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Type);
        Assert.Equal("Check6_ResxKeyFormat", results[0].CheckName);
        Assert.True(results[0].CanAutoFix);
        Assert.Contains("Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890", results[0].Message);
    }

    [Fact]
    public async Task Check6_PropertyNameKey_ReturnsEmpty()
    {
        // Arrange
        var filePath = CreateResxFile("EntitySystem.resx", new Dictionary<string, string>
        {
            ["Property_Name"] = "Название",
            ["Property_Status"] = "Статус"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { filePath })).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task Check6_MixedKeys_OnlyResourceGuidFlagged()
    {
        // Arrange: one bad Resource_GUID key and several good ones
        var filePath = CreateResxFile("EntitySystem.resx", new Dictionary<string, string>
        {
            ["Resource_11111111-2222-3333-4444-555555555555"] = "ИНН",
            ["Property_Name"] = "Название",
            ["Action_Send"] = "Отправить",
            ["DisplayName"] = "Контрагент"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { filePath })).ToList();

        // Assert: only the Resource_GUID key is flagged
        Assert.Single(results);
        Assert.Contains("Resource_11111111-2222-3333-4444-555555555555", results[0].Message);
    }

    [Fact]
    public async Task Check6_MultipleFilesWithResourceGuids_AllFlagged()
    {
        // Arrange
        var file1 = CreateResxFile("Entity1System.resx", new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Val1"
        });
        var file2 = CreateResxFile("Entity2System.resx", new Dictionary<string, string>
        {
            ["Resource_b2c3d4e5-f6a7-8901-bcde-f12345678901"] = "Val2"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { file1, file2 })).ToList();

        // Assert
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task Check6_ResourcePrefixNonGuid_NotFlagged()
    {
        // Arrange: "Resource_" prefix but not a GUID — should NOT be flagged
        var filePath = CreateResxFile("EntitySystem.resx", new Dictionary<string, string>
        {
            ["Resource_SomeTextKey"] = "Some value"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { filePath })).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public async Task Check6_EmptyResxFileArray_ReturnsEmpty()
    {
        var results = (await PackageValidator.Check6_ResxKeyFormat(Array.Empty<string>())).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public async Task Check6_MultipleResourceGuidsInOneFile_AllFlagged()
    {
        // Arrange
        var filePath = CreateResxFile("EntitySystem.resx", new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Val1",
            ["Resource_11111111-2222-3333-4444-555555555555"] = "Val2",
            ["Property_Name"] = "Название"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { filePath })).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.True(r.CanAutoFix));
    }

    #endregion

    #region Check7: Analyzers directory

    [Fact]
    public void Check7_DirectoryDoesNotExist_ReturnsWarning()
    {
        // Arrange: a fresh temp dir without .sds/Libraries/Analyzers
        var workDir = Path.Combine(_tempDir, "no_analyzers_" + Guid.NewGuid().ToString("N")[..6]);
        Directory.CreateDirectory(workDir);

        // Act
        var results = PackageValidator.Check7_AnalyzersDirectory(workDir).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Type);
        Assert.Equal("Check7_AnalyzersDirectory", results[0].CheckName);
        Assert.False(results[0].CanAutoFix);
        Assert.Contains("не найдена", results[0].Message);
    }

    [Fact]
    public void Check7_DirectoryExistsButNoDlls_ReturnsWarning()
    {
        // Arrange
        var workDir = Path.Combine(_tempDir, "empty_analyzers_" + Guid.NewGuid().ToString("N")[..6]);
        var analyzersDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        Directory.CreateDirectory(analyzersDir);

        // Act
        var results = PackageValidator.Check7_AnalyzersDirectory(workDir).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Type);
        Assert.Contains("не содержит DLL", results[0].Message);
    }

    [Fact]
    public void Check7_DirectoryExistsWithDlls_ReturnsEmpty()
    {
        // Arrange
        var workDir = Path.Combine(_tempDir, "full_analyzers_" + Guid.NewGuid().ToString("N")[..6]);
        var analyzersDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        Directory.CreateDirectory(analyzersDir);
        File.WriteAllBytes(Path.Combine(analyzersDir, "SomeAnalyzer.dll"), new byte[] { 0x4D, 0x5A }); // MZ header

        // Act
        var results = PackageValidator.Check7_AnalyzersDirectory(workDir).ToList();

        // Assert
        Assert.Empty(results);
    }

    [Fact]
    public void Check7_DirectoryExistsWithOnlyTxtFiles_ReturnsWarning()
    {
        // Arrange: has a .txt file but no .dll
        var workDir = Path.Combine(_tempDir, "txt_only_" + Guid.NewGuid().ToString("N")[..6]);
        var analyzersDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        Directory.CreateDirectory(analyzersDir);
        File.WriteAllText(Path.Combine(analyzersDir, "readme.txt"), "placeholder");

        // Act
        var results = PackageValidator.Check7_AnalyzersDirectory(workDir).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Type);
        Assert.Contains("не содержит DLL", results[0].Message);
    }

    [Fact]
    public void Check7_DirectoryExistsWithMultipleDlls_ReturnsEmpty()
    {
        // Arrange: multiple DLLs present
        var workDir = Path.Combine(_tempDir, "multi_dlls_" + Guid.NewGuid().ToString("N")[..6]);
        var analyzersDir = Path.Combine(workDir, ".sds", "Libraries", "Analyzers");
        Directory.CreateDirectory(analyzersDir);
        File.WriteAllBytes(Path.Combine(analyzersDir, "Analyzer1.dll"), new byte[] { 0x4D, 0x5A });
        File.WriteAllBytes(Path.Combine(analyzersDir, "Analyzer2.dll"), new byte[] { 0x4D, 0x5A });

        // Act
        var results = PackageValidator.Check7_AnalyzersDirectory(workDir).ToList();

        // Assert
        Assert.Empty(results);
    }

    #endregion

    #region Check9: DisplayName completeness

    [Fact]
    public void Check9_MissingDisplayName_ReturnsWarning()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000010",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "bbbbbbbb-0000-0000-0000-000000000010",
              "Name": "Title",
              "Code": "Title"
            }
          ]
        }
        """;
        var entities = ParseEntities(entityJson);

        // Create resx WITHOUT DisplayName
        var resxDir = Path.Combine(_tempDir, "check9_missing");
        Directory.CreateDirectory(resxDir);
        File.WriteAllText(Path.Combine(resxDir, "TestEntitySystem.ru.resx"),
            BuildResxXml(new Dictionary<string, string> { ["Property_Title"] = "Заголовок" }));

        var results = PackageValidator.Check9_DisplayNameCompleteness(entities, resxDir).ToList();

        Assert.Contains(results, r => r.Message.Contains("DisplayName"));
        Assert.All(results, r => Assert.Equal(ValidationSeverity.Warning, r.Type));
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check9_MissingPropertyKey_ReturnsWarning()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000011",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "bbbbbbbb-0000-0000-0000-000000000011",
              "Name": "Amount",
              "Code": "Amount"
            }
          ]
        }
        """;
        var entities = ParseEntities(entityJson);

        var resxDir = Path.Combine(_tempDir, "check9_prop");
        Directory.CreateDirectory(resxDir);
        File.WriteAllText(Path.Combine(resxDir, "TestEntitySystem.ru.resx"),
            BuildResxXml(new Dictionary<string, string> { ["DisplayName"] = "Тест" }));

        var results = PackageValidator.Check9_DisplayNameCompleteness(entities, resxDir).ToList();

        Assert.Contains(results, r => r.Message.Contains("Property_Amount"));
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check9_NoResxFile_ReturnsWarning()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000012",
          "Name": "OrphanEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": []
        }
        """;
        var entities = ParseEntities(entityJson);

        var emptyDir = Path.Combine(_tempDir, "check9_empty");
        Directory.CreateDirectory(emptyDir);

        var results = PackageValidator.Check9_DisplayNameCompleteness(entities, emptyDir).ToList();

        Assert.Single(results);
        Assert.Contains("не найден", results[0].Message);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check9_AllKeysPresent_ReturnsEmpty()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000013",
          "Name": "GoodEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "bbbbbbbb-0000-0000-0000-000000000013",
              "Name": "Name",
              "Code": "Name"
            }
          ]
        }
        """;
        var entities = ParseEntities(entityJson);

        var resxDir = Path.Combine(_tempDir, "check9_good");
        Directory.CreateDirectory(resxDir);
        File.WriteAllText(Path.Combine(resxDir, "GoodEntitySystem.ru.resx"),
            BuildResxXml(new Dictionary<string, string>
            {
                ["DisplayName"] = "Хорошая сущность",
                ["Property_Name"] = "Наименование"
            }));

        var results = PackageValidator.Check9_DisplayNameCompleteness(entities, resxDir).ToList();

        Assert.Empty(results);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region Check10: Empty Controls with Overridden

    [Fact]
    public void Check10_OverriddenControlsEmpty_ReturnsError()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000020",
          "Name": "EmptyFormEntity",
          "BaseGuid": "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
          "Overridden": ["Controls"],
          "Forms": [{
            "$type": "Sungero.Metadata.StandaloneFormMetadata",
            "NameGuid": "fa03f748-4397-42ef-bdc2-22119af7bf7f",
            "Name": "Card",
            "Controls": [],
            "Overridden": ["Controls"]
          }]
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check10_EmptyControlsWithOverridden(entities).ToList();

        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Type);
        Assert.Contains("пуст", results[0].Message);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check10_OverriddenControlsWithContent_ReturnsEmpty()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000021",
          "Name": "GoodFormEntity",
          "BaseGuid": "58cca102-1e97-4f07-b6ac-fd866a8b7cb1",
          "Overridden": ["Controls"],
          "Forms": [{
            "$type": "Sungero.Metadata.StandaloneFormMetadata",
            "NameGuid": "fa03f748-4397-42ef-bdc2-22119af7bf7f",
            "Name": "Card",
            "Controls": [{"$type": "Sungero.Metadata.ControlGroupMetadata", "NameGuid": "11111111-0000-0000-0000-000000000021", "Name": "Main"}],
            "Overridden": ["Controls"]
          }]
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check10_EmptyControlsWithOverridden(entities).ToList();

        Assert.Empty(results);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check10_NoOverridden_ReturnsEmpty()
    {
        var entities = ParseEntities(DatabookEntityNoCollection());

        var results = PackageValidator.Check10_EmptyControlsWithOverridden(entities).ToList();

        Assert.Empty(results);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region Check12: FormTabs detection

    [Fact]
    public void Check12_EntityWithFormTabs_ReturnsWarning()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000030",
          "Name": "TabEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "FormTabs": [{"Name": "Tab1"}]
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check12_FormTabsDetection(entities).ToList();

        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Warning, results[0].Type);
        Assert.Contains("FormTabs", results[0].Message);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check12_EntityWithoutFormTabs_ReturnsEmpty()
    {
        var entities = ParseEntities(DatabookEntityNoCollection());

        var results = PackageValidator.Check12_FormTabsDetection(entities).ToList();

        Assert.Empty(results);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region Check14: DomainApi version

    [Fact]
    public void Check14_MissingDomainApi_ReturnsError()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000040",
          "Name": "NoDomainApi",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Versions": [
            {"Type": "EntityMetadata", "Number": 13}
          ]
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check14_DomainApiVersion(entities).ToList();

        Assert.Single(results);
        Assert.Equal(ValidationSeverity.Error, results[0].Type);
        Assert.Contains("DomainApi", results[0].Message);
        Assert.True(results[0].CanAutoFix);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check14_HasDomainApi2_ReturnsEmpty()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000041",
          "Name": "GoodEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Versions": [
            {"Type": "EntityMetadata", "Number": 13},
            {"Type": "DomainApi", "Number": 2}
          ]
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check14_DomainApiVersion(entities).ToList();

        Assert.Empty(results);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check14_NoVersionsArray_ReturnsError()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000042",
          "Name": "NoVersions",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0"
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check14_DomainApiVersion(entities).ToList();

        Assert.Single(results);
        Assert.Contains("Versions", results[0].Message);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    [Fact]
    public void Check14_AutoGeneratedEntity_Skipped()
    {
        var entityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "aaaaaaaa-0000-0000-0000-000000000043",
          "Name": "AutoGenChild",
          "BaseGuid": "a3d38bf5-0414-41f6-bb33-a4621d2e5a60",
          "IsAutoGenerated": true,
          "Versions": []
        }
        """;
        var entities = ParseEntities(entityJson);

        var results = PackageValidator.Check14_DomainApiVersion(entities).ToList();

        Assert.Empty(results);
        foreach (var (_, doc) in entities) doc.Dispose();
    }

    #endregion

    #region ValidationResult record shape

    [Fact]
    public void ValidationResult_Check1_HasCorrectFilePath()
    {
        // Arrange
        var json = DatabookEntityWithCollection();
        var doc = JsonDocument.Parse(json);
        const string fakePath = "C:/repo/work/MyModule/MyEntity.mtd";
        var entities = new List<(string Path, JsonDocument Doc)> { (fakePath, doc) };

        // Act
        var results = PackageValidator.Check1_CollectionOnDatabookEntry(entities).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(fakePath, results[0].FilePath);

        doc.Dispose();
    }

    [Fact]
    public async Task ValidationResult_Check6_HasCorrectFilePath()
    {
        // Arrange
        var filePath = CreateResxFile("ModuleSystem.resx", new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Test"
        });

        // Act
        var results = (await PackageValidator.Check6_ResxKeyFormat(new[] { filePath })).ToList();

        // Assert
        Assert.Single(results);
        Assert.Equal(filePath, results[0].FilePath);
    }

    #endregion
}
