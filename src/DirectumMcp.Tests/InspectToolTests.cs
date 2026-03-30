using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class InspectToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly InspectTool _tool;

    // Sample entity MTD with DatabookEntry base, one enum property with reserved word, and no Actions list.
    private const string EntityMtdJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.EnumPropertyMetadata",
              "NameGuid": "11111111-1111-1111-1111-111111111111",
              "Name": "Status",
              "Code": "Status",
              "DirectValues": [
                { "NameGuid": "22222222-2222-2222-2222-222222222222", "Name": "new", "Code": "new" },
                { "NameGuid": "33333333-3333-3333-3333-333333333333", "Name": "Active", "Code": "Active" }
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

    private const string ModuleMtdJson = """
        {
          "$type": "Sungero.Metadata.ModuleMetadata",
          "NameGuid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          "Name": "TestModule",
          "Version": "1.2.3.4",
          "Dependencies": [
            { "Id": "cccccccc-cccc-cccc-cccc-cccccccccccc", "IsSolutionModule": false }
          ]
        }
        """;

    private const string SystemResxXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <resheader name="version"><value>2.0</value></resheader>
          <data name="Property_Status" xml:space="preserve">
            <value>Статус</value>
          </data>
          <data name="Action_Approve" xml:space="preserve">
            <value>Утвердить</value>
          </data>
          <data name="DisplayName" xml:space="preserve">
            <value>Тестовая сущность</value>
          </data>
        </root>
        """;

    private const string SystemResxWithResourceGuidXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <resheader name="version"><value>2.0</value></resheader>
          <data name="Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890" xml:space="preserve">
            <value>Status</value>
          </data>
        </root>
        """;

    public InspectToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "InspectToolTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new InspectTool();
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

    private string CreateSubDir(string name)
    {
        var path = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(path);
        return path;
    }

    #endregion

    #region Entity MTD

    [Fact]
    public async Task Inspect_EntityMtd_ReturnsFormattedMarkdown()
    {
        // Arrange
        var mtdPath = CreateFile("TestEntity.mtd", EntityMtdJson);

        // Act
        var result = await _tool.Inspect(mtdPath);

        // Assert: entity name, properties table header, actions table header present
        Assert.Contains("TestEntity", result);
        Assert.Contains("Свойства", result);
        Assert.Contains("Status", result);
        Assert.Contains("Действия", result);
        Assert.Contains("Approve", result);
    }

    [Fact]
    public async Task Inspect_EntityMtd_ReturnsEntityKindDatabookEntry()
    {
        var mtdPath = CreateFile("TestEntity.mtd", EntityMtdJson);

        var result = await _tool.Inspect(mtdPath);

        // BaseGuid 04581d26-0780-4cfd-b3cd-c2cafc5798b0 => DatabookEntry
        Assert.Contains("DatabookEntry", result);
    }

    [Fact]
    public async Task Inspect_EntityMtd_ReturnsEnumDirectValues()
    {
        var mtdPath = CreateFile("TestEntity.mtd", EntityMtdJson);

        var result = await _tool.Inspect(mtdPath);

        // DirectValues should appear in the properties table extras column
        Assert.Contains("new", result);
        Assert.Contains("Active", result);
    }

    #endregion

    #region Module MTD

    [Fact]
    public async Task Inspect_ModuleMtd_ReturnsModuleInfo()
    {
        // Arrange
        var mtdPath = CreateFile("Module.mtd", ModuleMtdJson);

        // Act
        var result = await _tool.Inspect(mtdPath);

        // Assert
        Assert.Contains("TestModule", result);
        Assert.Contains("Модуль", result);
        Assert.Contains("1.2.3.4", result);
    }

    [Fact]
    public async Task Inspect_ModuleMtd_ReturnsDependencies()
    {
        var mtdPath = CreateFile("Module.mtd", ModuleMtdJson);

        var result = await _tool.Inspect(mtdPath);

        Assert.Contains("Зависимости", result);
        Assert.Contains("cccccccc-cccc-cccc-cccc-cccccccccccc", result);
    }

    #endregion

    #region Resx Files

    [Fact]
    public async Task Inspect_ResxFile_ReturnsKeyCategories()
    {
        // Arrange
        var resxPath = CreateFile("TestEntitySystem.resx", SystemResxXml);

        // Act
        var result = await _tool.Inspect(resxPath);

        // Assert: key categories section exists
        Assert.Contains("Ключи по категориям", result);
        Assert.Contains("Property_", result);
        Assert.Contains("Action_", result);
    }

    [Fact]
    public async Task Inspect_ResxFile_ReturnsFileName()
    {
        var resxPath = CreateFile("TestEntitySystem.resx", SystemResxXml);

        var result = await _tool.Inspect(resxPath);

        Assert.Contains("TestEntitySystem.resx", result);
    }

    [Fact]
    public async Task Inspect_ResxWithResourceGuid_FlagsProblems()
    {
        // Arrange: resx with Resource_<GUID> key pattern (known DDS 25.3 issue)
        var resxPath = CreateFile("TestEntitySystem.resx", SystemResxWithResourceGuidXml);

        // Act
        var result = await _tool.Inspect(resxPath);

        // Assert: Problems section appears
        Assert.Contains("Проблемы", result);
        Assert.Contains("Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890", result);
        Assert.Contains("Property_<Name>", result);
    }

    [Fact]
    public async Task Inspect_ResxWithValidKeys_NoProblemSection()
    {
        var resxPath = CreateFile("TestEntitySystem.resx", SystemResxXml);

        var result = await _tool.Inspect(resxPath);

        Assert.DoesNotContain("Проблемы", result);
    }

    #endregion

    #region Directory Inspection

    [Fact]
    public async Task Inspect_Directory_ReturnsEntityTable()
    {
        // Arrange: directory with two entity MTD files
        var moduleDir = CreateSubDir("MyModule");
        File.WriteAllText(Path.Combine(moduleDir, "TestEntity.mtd"), EntityMtdJson);

        var secondEntityJson = EntityMtdJson
            .Replace("\"Name\": \"TestEntity\"", "\"Name\": \"AnotherEntity\"")
            .Replace("a1b2c3d4-e5f6-7890-abcd-ef1234567890", "a1b2c3d4-e5f6-7890-abcd-ef1234567891");
        File.WriteAllText(Path.Combine(moduleDir, "AnotherEntity.mtd"), secondEntityJson);

        // Act
        var result = await _tool.Inspect(moduleDir);

        // Assert: entity table with both entities
        Assert.Contains("Сущности", result);
        Assert.Contains("TestEntity", result);
        Assert.Contains("AnotherEntity", result);
    }

    [Fact]
    public async Task Inspect_Directory_WithModuleMtd_ReturnsModuleDependencies()
    {
        // Arrange
        var moduleDir = CreateSubDir("DepModule");
        File.WriteAllText(Path.Combine(moduleDir, "Module.mtd"), ModuleMtdJson);
        File.WriteAllText(Path.Combine(moduleDir, "TestEntity.mtd"), EntityMtdJson);

        // Act
        var result = await _tool.Inspect(moduleDir);

        // Assert: module dependencies section present
        Assert.Contains("Зависимости", result);
        Assert.Contains("cccccccc-cccc-cccc-cccc-cccccccccccc", result);
    }

    [Fact]
    public async Task Inspect_Directory_ReturnsFileStructureSection()
    {
        var moduleDir = CreateSubDir("StructModule");
        // Create server and client subdirs with .cs files
        var serverDir = Path.Combine(moduleDir, "Server");
        Directory.CreateDirectory(serverDir);
        File.WriteAllText(Path.Combine(serverDir, "Functions.cs"), "// server code");

        // Act
        var result = await _tool.Inspect(moduleDir);

        // Assert: file structure section present
        Assert.Contains("Структура файлов", result);
        Assert.Contains("Server/", result);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Inspect_NonexistentPath_ReturnsError()
    {
        var nonexistentPath = Path.Combine(_tempDir, "does_not_exist.mtd");

        var result = await _tool.Inspect(nonexistentPath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найден", result);
    }

    [Fact]
    public async Task Inspect_UnsupportedExtension_ReturnsError()
    {
        var txtPath = CreateFile("readme.txt", "hello");

        var result = await _tool.Inspect(txtPath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains(".txt", result);
    }

    [Fact]
    public async Task Inspect_PathOutsideSolutionPath_ReturnsDenied()
    {
        // _tempDir is inside Path.GetTempPath() which is always allowed by IsPathAllowed.
        // To test denial, we need a path outside both SOLUTION_PATH and temp.
        // Use the Windows directory (or /usr on Linux) which exists but is clearly outside allowed.
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr"; // fallback for non-Windows

        if (!Directory.Exists(outsidePath))
            return; // skip if neither exists

        var result = await _tool.Inspect(outsidePath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    [Fact]
    public async Task Inspect_NoSolutionPathEnvVar_ReturnsDenied()
    {
        // Arrange: clear SOLUTION_PATH completely
        Environment.SetEnvironmentVariable("SOLUTION_PATH", null);
        try
        {
            var mtdPath = CreateFile("TestEntity.mtd", EntityMtdJson);

            // Act
            var result = await _tool.Inspect(mtdPath);

            // Assert
            Assert.Contains("ОШИБКА", result);
            Assert.Contains("запрещён", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        }
    }

    #endregion
}
