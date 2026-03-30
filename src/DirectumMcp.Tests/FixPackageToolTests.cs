using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class FixPackageToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly FixPackageTool _tool;

    // Entity with a reserved C# word "new" as an enum value name (Check 3).
    private const string EntityWithReservedEnumJson = """
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
          "Actions": []
        }
        """;

    // Assignment entity with non-empty Constraints on an associated attachment group (Check 5).
    private const string AssignmentWithConstraintsJson = """
        {
          "$type": "Sungero.Metadata.AssignmentMetadata",
          "NameGuid": "b1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "TestAssignment",
          "BaseGuid": "91cbfdc8-5d5d-465e-95a4-3a987e1a0c24",
          "Properties": [],
          "Actions": [],
          "AttachmentGroups": [
            {
              "NameGuid": "c1c2c3d4-e5f6-7890-abcd-ef1234567890",
              "Name": "MainGroup",
              "IsAssociatedEntityGroup": true,
              "Constraints": [
                { "Name": "SomeConstraint", "EntityGuid": "d1d2d3d4-d5d6-d7d8-d9da-dbdcdddedfe0" }
              ]
            }
          ]
        }
        """;

    // Clean entity MTD — no reserved enums, no problematic constructs.
    private const string CleanEntityJson = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "e1e2e3e4-e5e6-e7e8-e9ea-ebecedeeeff0",
          "Name": "CleanEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [
            {
              "$type": "Sungero.Metadata.StringPropertyMetadata",
              "NameGuid": "f1f2f3f4-f5f6-f7f8-f9fa-fbfcfdfeff00",
              "Name": "Name",
              "Code": "Name",
              "IsRequired": true
            }
          ],
          "Actions": [],
          "Versions": [
            {"Type": "EntityMetadata", "Number": 13},
            {"Type": "DomainApi", "Number": 2}
          ]
        }
        """;

    // Clean System.resx with valid Property_ keys.
    private const string CleanSystemResxXml = """
        <?xml version="1.0" encoding="utf-8"?>
        <root>
          <resheader name="resmimetype"><value>text/microsoft-resx</value></resheader>
          <resheader name="version"><value>2.0</value></resheader>
          <data name="Property_Name" xml:space="preserve">
            <value>Название</value>
          </data>
          <data name="DisplayName" xml:space="preserve">
            <value>Чистая сущность</value>
          </data>
        </root>
        """;

    // System.resx with a Resource_<GUID> key that must be renamed (Check 6).
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

    public FixPackageToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "FixPackageToolTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new FixPackageTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Helpers

    private string CreatePackageDir(string name)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string WriteMtd(string dir, string fileName, string content)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string WriteResx(string dir, string fileName, string content)
    {
        var path = Path.Combine(dir, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    #endregion

    #region DryRun behavior

    [Fact]
    public async Task Fix_DryRun_NoFileChanges()
    {
        // Arrange: package directory with an entity containing a reserved enum name
        var packageDir = CreatePackageDir("pkg_dryrun");
        var mtdPath = WriteMtd(packageDir, "TestEntity.mtd", EntityWithReservedEnumJson);
        var originalContent = File.ReadAllText(mtdPath);

        // Act: dryRun=true (default)
        var report = await _tool.Fix(packageDir, dryRun: true);

        // Assert: report mentions the issue but file is unchanged
        Assert.Contains("Check3", report);
        Assert.Contains("new", report);
        var contentAfter = File.ReadAllText(mtdPath);
        Assert.Equal(originalContent, contentAfter);
    }

    [Fact]
    public async Task Fix_DryRun_ReportContainsPreviewMode()
    {
        var packageDir = CreatePackageDir("pkg_dryrun_mode");
        WriteMtd(packageDir, "TestEntity.mtd", EntityWithReservedEnumJson);

        var report = await _tool.Fix(packageDir, dryRun: true);

        Assert.Contains("dryRun=true", report);
        Assert.Contains("dryRun=false", report); // invitation to apply
    }

    #endregion

    #region Check 3 — Reserved Enum Names

    [Fact]
    public async Task Fix_Check3_FixesReservedEnum()
    {
        // Arrange
        var packageDir = CreatePackageDir("pkg_check3");
        var mtdPath = WriteMtd(packageDir, "TestEntity.mtd", EntityWithReservedEnumJson);

        // Act: apply fixes
        var report = await _tool.Fix(packageDir, dryRun: false);

        // Assert report mentions fix
        Assert.Contains("Check3", report);

        // Assert file has been modified: "new" -> "newValue"
        var modifiedContent = File.ReadAllText(mtdPath);
        Assert.Contains("newValue", modifiedContent);
        // The old plain "new" value name should be gone from DirectValues
        // (it will still appear as part of "newValue" — check it is no longer a standalone "new")
        Assert.DoesNotContain("\"Name\": \"new\"", modifiedContent);
    }

    [Fact]
    public async Task Fix_Check3_LeavesNonReservedValuesUntouched()
    {
        var packageDir = CreatePackageDir("pkg_check3_nonreserved");
        var mtdPath = WriteMtd(packageDir, "TestEntity.mtd", EntityWithReservedEnumJson);

        await _tool.Fix(packageDir, dryRun: false);

        var modifiedContent = File.ReadAllText(mtdPath);
        // "Active" is not a reserved word and must survive unchanged
        Assert.Contains("Active", modifiedContent);
    }

    #endregion

    #region Check 5 — AttachmentGroup Constraints

    [Fact]
    public async Task Fix_Check5_FixesConstraints()
    {
        // Arrange: assignment entity with non-empty Constraints on associated group
        var packageDir = CreatePackageDir("pkg_check5");
        var mtdPath = WriteMtd(packageDir, "TestAssignment.mtd", AssignmentWithConstraintsJson);

        // Act
        var report = await _tool.Fix(packageDir, dryRun: false);

        // Assert: Check5 appears in report
        Assert.Contains("Check5", report);

        // Assert: Constraints array is now empty in the file
        var modifiedContent = File.ReadAllText(mtdPath);
        // The fixed file should have "Constraints": [] (empty array)
        Assert.DoesNotContain("SomeConstraint", modifiedContent);
    }

    [Fact]
    public async Task Fix_Check5_DryRun_ReportsButDoesNotModify()
    {
        var packageDir = CreatePackageDir("pkg_check5_dry");
        var mtdPath = WriteMtd(packageDir, "TestAssignment.mtd", AssignmentWithConstraintsJson);
        var originalContent = File.ReadAllText(mtdPath);

        var report = await _tool.Fix(packageDir, dryRun: true);

        Assert.Contains("Check5", report);
        Assert.Equal(originalContent, File.ReadAllText(mtdPath));
    }

    #endregion

    #region Check 6 — System.resx Key Format

    [Fact]
    public async Task Fix_Check6_FixesResxKeys()
    {
        // Arrange: entity MTD + matching System.resx with Resource_GUID key
        var packageDir = CreatePackageDir("pkg_check6");
        WriteMtd(packageDir, "TestEntity.mtd", EntityWithReservedEnumJson);
        var resxPath = WriteResx(packageDir, "TestEntitySystem.resx", SystemResxWithResourceGuidXml);

        // Act
        var report = await _tool.Fix(packageDir, dryRun: false);

        // Assert: Check6 in report
        Assert.Contains("Check6", report);

        // Assert: Resource_GUID key is now gone from the resx file
        var modifiedContent = File.ReadAllText(resxPath);
        Assert.DoesNotContain("Resource_a1b2c3d4-e5f6-7890-abcd-ef1234567890", modifiedContent);
        // The new key should be Property_Status (value in the resx is "Status" which matches property name)
        Assert.Contains("Property_Status", modifiedContent);
    }

    [Fact]
    public async Task Fix_Check6_DryRun_ResxFileUnchanged()
    {
        var packageDir = CreatePackageDir("pkg_check6_dry");
        WriteMtd(packageDir, "TestEntity.mtd", EntityWithReservedEnumJson);
        var resxPath = WriteResx(packageDir, "TestEntitySystem.resx", SystemResxWithResourceGuidXml);
        var originalContent = File.ReadAllText(resxPath);

        await _tool.Fix(packageDir, dryRun: true);

        Assert.Equal(originalContent, File.ReadAllText(resxPath));
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Fix_NonexistentPath_ReturnsError()
    {
        var nonexistentPath = Path.Combine(_tempDir, "no_such_package");

        var result = await _tool.Fix(nonexistentPath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найден", result);
    }

    [Fact]
    public async Task Fix_PathOutsideSolutionPath_ReturnsDenied()
    {
        // IsPathAllowed also allows everything under Path.GetTempPath().
        // _tempDir lives under temp, so changing SOLUTION_PATH to another temp subdirectory
        // would NOT cause a denial. We need a path that is outside both SOLUTION_PATH and temp.
        // Use the Windows system directory on Windows, or /usr on Linux/macOS.
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr"; // fallback for non-Windows

        if (!Directory.Exists(outsidePath))
            return; // skip if neither system path is available

        // Act
        var result = await _tool.Fix(outsidePath);

        // Assert: access denied
        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    [Fact]
    public async Task Fix_NoSolutionPathEnvVar_ReturnsDenied()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", null);
        try
        {
            var packageDir = CreatePackageDir("pkg_no_env");
            var result = await _tool.Fix(packageDir);

            Assert.Contains("ОШИБКА", result);
            Assert.Contains("запрещён", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        }
    }

    #endregion

    #region Clean Package

    [Fact]
    public async Task Fix_CleanPackage_NoIssues()
    {
        // Arrange: valid MTD + clean System.resx — nothing to fix
        var packageDir = CreatePackageDir("pkg_clean");
        WriteMtd(packageDir, "CleanEntity.mtd", CleanEntityJson);
        WriteResx(packageDir, "CleanEntitySystem.resx", CleanSystemResxXml);
        // Also add the .sds/Libraries/Analyzers directory to satisfy Check 7
        var analyzersDir = Path.Combine(packageDir, ".sds", "Libraries", "Analyzers");
        Directory.CreateDirectory(analyzersDir);
        File.WriteAllText(Path.Combine(analyzersDir, "placeholder.dll"), "");

        // Act
        var report = await _tool.Fix(packageDir, dryRun: true);

        // Assert: auto-fixed count is 0
        Assert.Contains("Исправлено автоматически (0)", report);
    }

    [Fact]
    public async Task Fix_CleanPackage_ReportContainsSummary()
    {
        var packageDir = CreatePackageDir("pkg_clean_summary");
        WriteMtd(packageDir, "CleanEntity.mtd", CleanEntityJson);

        var report = await _tool.Fix(packageDir, dryRun: true);

        Assert.Contains("Итого", report);
        Assert.Contains("Исправлено автоматически:", report);
        Assert.Contains("Требует ручного исправления:", report);
    }

    #endregion

    #region Check 7 — Analyzers directory (manual issue)

    [Fact]
    public async Task Fix_MissingAnalyzersDir_ReportsManualIssue()
    {
        // Arrange: package without .sds/Libraries/Analyzers
        var packageDir = CreatePackageDir("pkg_no_analyzers");
        WriteMtd(packageDir, "CleanEntity.mtd", CleanEntityJson);
        // Deliberately do NOT create the Analyzers directory

        // Act
        var report = await _tool.Fix(packageDir, dryRun: true);

        // Assert: Check7 appears in the manual issues section
        Assert.Contains("Check7", report);
        Assert.Contains("Analyzers", report);
    }

    #endregion
}
