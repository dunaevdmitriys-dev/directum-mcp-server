using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class DiffPackagesToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly DiffPackagesTool _tool;

    private const string EntityMtdA = """
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
          "Actions": [
            { "NameGuid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Name": "Save" }
          ]
        }
        """;

    private const string EntityMtdB = """
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
              "NameGuid": "22222222-2222-2222-2222-222222222222",
              "Name": "Description",
              "Code": "Description"
            }
          ],
          "Actions": [
            { "NameGuid": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Name": "Save" },
            { "NameGuid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Name": "Approve" }
          ]
        }
        """;

    private const string ResxA = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="Property_Title" xml:space="preserve">
            <value>Заголовок</value>
          </data>
        </root>
        """;

    private const string ResxB = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <data name="Property_Title" xml:space="preserve">
            <value>Название</value>
          </data>
          <data name="Property_Description" xml:space="preserve">
            <value>Описание</value>
          </data>
        </root>
        """;

    public DiffPackagesToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DiffPkgTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new DiffPackagesTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

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

    #region Identical Packages

    [Fact]
    public async Task Diff_IdenticalPackages_NoChanges()
    {
        var dirA = CreateDir("pkgA_same");
        var dirB = CreateDir("pkgB_same");
        WriteFile(dirA, "Entity.mtd", EntityMtdA);
        WriteFile(dirB, "Entity.mtd", EntityMtdA);

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Без изменений: 1", result);
        Assert.Contains("Добавлено (0)", result);
        Assert.Contains("Удалено (0)", result);
        Assert.Contains("Изменено (0)", result);
    }

    #endregion

    #region Added/Removed Files

    [Fact]
    public async Task Diff_FileAddedInB_ShowsAdded()
    {
        var dirA = CreateDir("pkgA_added");
        var dirB = CreateDir("pkgB_added");
        WriteFile(dirA, "Entity.mtd", EntityMtdA);
        WriteFile(dirB, "Entity.mtd", EntityMtdA);
        WriteFile(dirB, "NewEntity.mtd", EntityMtdB);

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Добавлено (1)", result);
        Assert.Contains("NewEntity.mtd", result);
    }

    [Fact]
    public async Task Diff_FileRemovedInB_ShowsRemoved()
    {
        var dirA = CreateDir("pkgA_removed");
        var dirB = CreateDir("pkgB_removed");
        WriteFile(dirA, "Entity.mtd", EntityMtdA);
        WriteFile(dirA, "OldEntity.mtd", EntityMtdB);
        WriteFile(dirB, "Entity.mtd", EntityMtdA);

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Удалено (1)", result);
        Assert.Contains("OldEntity.mtd", result);
    }

    #endregion

    #region MTD Changes

    [Fact]
    public async Task Diff_MtdPropertyAdded_ShowsDetails()
    {
        var dirA = CreateDir("pkgA_mtd");
        var dirB = CreateDir("pkgB_mtd");
        WriteFile(dirA, "Entity.mtd", EntityMtdA);
        WriteFile(dirB, "Entity.mtd", EntityMtdB);

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Изменено (1)", result);
        Assert.Contains("+1 свойств", result);
        Assert.Contains("+1 действий", result);
    }

    #endregion

    #region RESX Changes

    [Fact]
    public async Task Diff_ResxKeysChanged_ShowsDetails()
    {
        var dirA = CreateDir("pkgA_resx");
        var dirB = CreateDir("pkgB_resx");
        WriteFile(dirA, "EntitySystem.resx", ResxA);
        WriteFile(dirB, "EntitySystem.resx", ResxB);

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Изменено (1)", result);
        Assert.Contains("+1 ключей", result);
        Assert.Contains("~1 изменено", result);
    }

    #endregion

    #region CS Changes

    [Fact]
    public async Task Diff_CsFileChanged_ShowsLineCount()
    {
        var dirA = CreateDir("pkgA_cs");
        var dirB = CreateDir("pkgB_cs");
        WriteFile(dirA, "Server/Functions.cs", "// line1\n// line2\n// line3\n");
        WriteFile(dirB, "Server/Functions.cs", "// line1\n// line2\n// line3\n// line4\n// line5\n");

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Изменено (1)", result);
        Assert.Contains("строк", result);
    }

    #endregion

    #region Scope Filter

    [Fact]
    public async Task Diff_ScopeMetadata_OnlyShowsMtd()
    {
        var dirA = CreateDir("pkgA_scope");
        var dirB = CreateDir("pkgB_scope");
        WriteFile(dirA, "Entity.mtd", EntityMtdA);
        WriteFile(dirA, "EntitySystem.resx", ResxA);
        WriteFile(dirB, "Entity.mtd", EntityMtdB);
        WriteFile(dirB, "EntitySystem.resx", ResxB);

        var result = await _tool.DiffPackages(dirA, dirB, scope: "metadata");

        Assert.Contains("Изменено (1)", result);
        Assert.DoesNotContain("Ресурсы", result);
    }

    #endregion

    #region Summary

    [Fact]
    public async Task Diff_Report_ContainsSummarySection()
    {
        var dirA = CreateDir("pkgA_summary");
        var dirB = CreateDir("pkgB_summary");
        WriteFile(dirA, "Entity.mtd", EntityMtdA);
        WriteFile(dirB, "Entity.mtd", EntityMtdA);

        var result = await _tool.DiffPackages(dirA, dirB);

        Assert.Contains("Итого", result);
        Assert.Contains("Добавлено:", result);
        Assert.Contains("Удалено:", result);
        Assert.Contains("Изменено:", result);
        Assert.Contains("Без изменений:", result);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Diff_NonexistentPathA_ReturnsError()
    {
        var dirB = CreateDir("pkgB_err");
        WriteFile(dirB, "Entity.mtd", EntityMtdA);

        var result = await _tool.DiffPackages(Path.Combine(_tempDir, "no_such"), dirB);

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Diff_InvalidScope_ReturnsError()
    {
        var dirA = CreateDir("pkgA_bad_scope");
        var dirB = CreateDir("pkgB_bad_scope");

        var result = await _tool.DiffPackages(dirA, dirB, scope: "invalid");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("invalid", result);
    }

    [Fact]
    public async Task Diff_PathOutsideSolutionPath_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr";
        if (!Directory.Exists(outsidePath))
            return;

        var dirB = CreateDir("pkgB_denied");

        var result = await _tool.DiffPackages(outsidePath, dirB);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion
}
