using DirectumMcp.Core.Parsers;
using Xunit;

namespace DirectumMcp.Tests;

public class ResxValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public ResxValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DirectumMcpTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Helpers

    private string CreateResxFile(string fileName, Dictionary<string, string> entries)
    {
        var filePath = Path.Combine(_tempDir, fileName);
        var xml = BuildResxXml(entries);
        File.WriteAllText(filePath, xml);
        return filePath;
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

    #endregion

    #region Valid Key Detection

    [Fact]
    public async Task ValidateSystemResxAsync_PropertyKeys_NoIssues()
    {
        // Arrange
        var entries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Название",
            ["Property_TIN"] = "ИНН",
            ["Property_Counterparty"] = "Контрагент"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        // Act
        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        // Assert
        Assert.Empty(issues);
    }

    [Fact]
    public async Task ValidateSystemResxAsync_ActionKeys_NoIssues()
    {
        var entries = new Dictionary<string, string>
        {
            ["Action_Send"] = "Отправить",
            ["Action_Approve"] = "Утвердить"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        Assert.Empty(issues);
    }

    [Fact]
    public async Task ValidateSystemResxAsync_MixedValidKeys_NoIssues()
    {
        var entries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Название",
            ["Action_Send"] = "Отправить",
            ["Enum_Status_Active"] = "Действующий",
            ["DisplayName"] = "Договор",
            ["CollectionDisplayName"] = "Договоры"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        Assert.Empty(issues);
    }

    #endregion

    #region Invalid Resource_GUID Key Detection

    [Fact]
    public async Task ValidateSystemResxAsync_ResourceGuidKey_DetectedAsIssue()
    {
        var entries = new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Название"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        Assert.Single(issues);
        Assert.Contains("Resource_", issues[0].Key);
        Assert.Contains("Resource_<GUID>", issues[0].Message);
    }

    [Fact]
    public async Task ValidateSystemResxAsync_MultipleResourceGuidKeys_AllDetected()
    {
        var entries = new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Название",
            ["Resource_11111111-2222-3333-4444-555555555555"] = "ИНН",
            ["Property_Status"] = "Статус"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        Assert.Equal(2, issues.Count);
        Assert.All(issues, issue => Assert.StartsWith("Resource_", issue.Key));
    }

    [Fact]
    public async Task ValidateSystemResxAsync_ResourceGuidKey_ContainsFilePath()
    {
        var entries = new Dictionary<string, string>
        {
            ["Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890"] = "Тест"
        };
        var filePath = CreateResxFile("TestSystem.resx", entries);

        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        Assert.Single(issues);
        Assert.Equal(filePath, issues[0].FilePath);
    }

    [Fact]
    public async Task ValidateSystemResxAsync_ResourceNonGuid_NotDetected()
    {
        // Resource_ prefix but NOT a GUID suffix -- should NOT be flagged
        var entries = new Dictionary<string, string>
        {
            ["Resource_SomeTextKey"] = "Some value"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var issues = await ResxParser.ValidateSystemResxAsync(filePath);

        Assert.Empty(issues);
    }

    #endregion

    #region Missing Translation Detection

    [Fact]
    public async Task ParseAsync_ComparingTwoFiles_DetectsMissingTranslations()
    {
        // Arrange: main resx has 3 keys, localized resx has only 2
        var mainEntries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Name",
            ["Property_TIN"] = "TIN",
            ["Property_Status"] = "Status"
        };
        var localizedEntries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Название",
            ["Property_TIN"] = "ИНН"
            // Property_Status is missing
        };

        var mainPath = CreateResxFile("DealSystem.resx", mainEntries);
        var localizedPath = CreateResxFile("DealSystem.ru.resx", localizedEntries);

        // Act
        var mainKeys = await ResxParser.ParseAsync(mainPath);
        var localizedKeys = await ResxParser.ParseAsync(localizedPath);

        var missingKeys = mainKeys.Keys
            .Where(k => !localizedKeys.ContainsKey(k))
            .ToList();

        // Assert
        Assert.Single(missingKeys);
        Assert.Equal("Property_Status", missingKeys[0]);
    }

    [Fact]
    public async Task ParseAsync_IdenticalFiles_NoMissingTranslations()
    {
        var entries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Name",
            ["Property_TIN"] = "TIN"
        };
        var localizedEntries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Название",
            ["Property_TIN"] = "ИНН"
        };

        var mainPath = CreateResxFile("DealSystem.resx", entries);
        var localizedPath = CreateResxFile("DealSystem.ru.resx", localizedEntries);

        var mainKeys = await ResxParser.ParseAsync(mainPath);
        var localizedKeys = await ResxParser.ParseAsync(localizedPath);

        var missingKeys = mainKeys.Keys
            .Where(k => !localizedKeys.ContainsKey(k))
            .ToList();

        Assert.Empty(missingKeys);
    }

    [Fact]
    public async Task ParseAsync_EmptyLocalization_AllKeysMissing()
    {
        var mainEntries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Name",
            ["Property_TIN"] = "TIN"
        };
        var localizedEntries = new Dictionary<string, string>();

        var mainPath = CreateResxFile("DealSystem.resx", mainEntries);
        var localizedPath = CreateResxFile("DealSystem.ru.resx", localizedEntries);

        var mainKeys = await ResxParser.ParseAsync(mainPath);
        var localizedKeys = await ResxParser.ParseAsync(localizedPath);

        var missingKeys = mainKeys.Keys
            .Where(k => !localizedKeys.ContainsKey(k))
            .ToList();

        Assert.Equal(2, missingKeys.Count);
    }

    #endregion

    #region GetPropertyKeysAsync

    [Fact]
    public async Task GetPropertyKeysAsync_MixedKeys_ReturnsOnlyPropertyKeys()
    {
        var entries = new Dictionary<string, string>
        {
            ["Property_Name"] = "Название",
            ["Action_Send"] = "Отправить",
            ["Property_Status"] = "Статус",
            ["DisplayName"] = "Договор"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var propertyKeys = await ResxParser.GetPropertyKeysAsync(filePath);

        Assert.Equal(2, propertyKeys.Count);
        Assert.Contains("Property_Name", propertyKeys);
        Assert.Contains("Property_Status", propertyKeys);
        Assert.DoesNotContain("Action_Send", propertyKeys);
        Assert.DoesNotContain("DisplayName", propertyKeys);
    }

    [Fact]
    public async Task GetPropertyKeysAsync_NoPropertyKeys_ReturnsEmpty()
    {
        var entries = new Dictionary<string, string>
        {
            ["Action_Send"] = "Отправить",
            ["DisplayName"] = "Договор"
        };
        var filePath = CreateResxFile("DealSystem.resx", entries);

        var propertyKeys = await ResxParser.GetPropertyKeysAsync(filePath);

        Assert.Empty(propertyKeys);
    }

    #endregion

    #region ParseAsync Basics

    [Fact]
    public async Task ParseAsync_ValidResx_ReturnsAllEntries()
    {
        var entries = new Dictionary<string, string>
        {
            ["Key1"] = "Value1",
            ["Key2"] = "Value2"
        };
        var filePath = CreateResxFile("Test.resx", entries);

        var result = await ResxParser.ParseAsync(filePath);

        Assert.Equal(2, result.Count);
        Assert.Equal("Value1", result["Key1"]);
        Assert.Equal("Value2", result["Key2"]);
    }

    [Fact]
    public async Task ParseAsync_EmptyResx_ReturnsEmptyDictionary()
    {
        var entries = new Dictionary<string, string>();
        var filePath = CreateResxFile("Empty.resx", entries);

        var result = await ResxParser.ParseAsync(filePath);

        Assert.Empty(result);
    }

    #endregion
}
