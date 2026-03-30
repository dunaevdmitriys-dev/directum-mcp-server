using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class CheckCodeConsistencyToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly CheckCodeConsistencyTool _tool;

    private const string EntityMtdWithFunctions = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
          "Name": "TestEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [],
          "Actions": [],
          "PublicFunctions": [
            {
              "$type": "Sungero.Metadata.FunctionMetadata",
              "NameGuid": "11111111-1111-1111-1111-111111111111",
              "Name": "GetActiveItems"
            },
            {
              "$type": "Sungero.Metadata.FunctionMetadata",
              "NameGuid": "22222222-2222-2222-2222-222222222222",
              "Name": "ValidateEntity"
            }
          ]
        }
        """;

    private const string ModuleMtdWithCoverAction = """
        {
          "$type": "Sungero.Metadata.ModuleMetadata",
          "NameGuid": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb",
          "Name": "TestModule",
          "Version": "1.0.0.0",
          "Actions": [
            {
              "$type": "Sungero.Metadata.CoverFunctionActionMetadata",
              "NameGuid": "cccccccc-cccc-cccc-cccc-cccccccccccc",
              "Name": "ShowDashboard",
              "FunctionName": "ShowDashboardDialog"
            }
          ]
        }
        """;

    private const string CleanEntityMtd = """
        {
          "$type": "Sungero.Metadata.EntityMetadata",
          "NameGuid": "e1e2e3e4-e5e6-e7e8-e9ea-ebecedeeeff0",
          "Name": "CleanEntity",
          "BaseGuid": "04581d26-0780-4cfd-b3cd-c2cafc5798b0",
          "Properties": [],
          "Actions": []
        }
        """;

    public CheckCodeConsistencyToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CheckCodeTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new CheckCodeConsistencyTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreatePackageDir(string name)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFile(string dir, string relativePath, string content)
    {
        var fullPath = Path.Combine(dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    #region Check 1 — Server Functions

    [Fact]
    public async Task Check_ServerFunction_Missing_ReportsIssue()
    {
        var pkg = CreatePackageDir("pkg_server_func");
        WriteFile(pkg, "TestEntity.mtd", EntityMtdWithFunctions);
        // Only one of two functions exists
        WriteFile(pkg, "Server/Functions.cs", """
            namespace MyModule.Server
            {
                public partial class TestEntity
                {
                    public void GetActiveItems() { }
                }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("ServerFunctions", result);
        Assert.Contains("ValidateEntity", result);
        Assert.DoesNotContain("GetActiveItems", result.Split("Проблемы")[^1].Split("Итого")[0]);
    }

    [Fact]
    public async Task Check_ServerFunction_AllPresent_NoIssue()
    {
        var pkg = CreatePackageDir("pkg_server_func_ok");
        WriteFile(pkg, "TestEntity.mtd", EntityMtdWithFunctions);
        WriteFile(pkg, "Server/Functions.cs", """
            namespace MyModule.Server
            {
                public partial class TestEntity
                {
                    public void GetActiveItems() { }
                    public bool ValidateEntity(int id) { return true; }
                }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.DoesNotContain("ServerFunctions", result);
    }

    #endregion

    #region Check 3 — Client Functions (Cover Actions)

    [Fact]
    public async Task Check_CoverClientFunction_Missing_ReportsIssue()
    {
        var pkg = CreatePackageDir("pkg_cover_func");
        WriteFile(pkg, "Module.mtd", ModuleMtdWithCoverAction);
        // No client functions file at all

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("ClientFunctions", result);
        Assert.Contains("ShowDashboardDialog", result);
    }

    [Fact]
    public async Task Check_CoverClientFunction_Present_NoIssue()
    {
        var pkg = CreatePackageDir("pkg_cover_func_ok");
        WriteFile(pkg, "Module.mtd", ModuleMtdWithCoverAction);
        WriteFile(pkg, "ModuleClientFunctions.cs", """
            namespace MyModule.Client
            {
                public partial class Module
                {
                    public void ShowDashboardDialog() { }
                }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.DoesNotContain("ClientFunctions", result);
    }

    #endregion

    #region Check 4 — Partial Class Name

    [Fact]
    public async Task Check_PartialClass_Missing_ReportsIssue()
    {
        var pkg = CreatePackageDir("pkg_partial_class");
        WriteFile(pkg, "TestEntity.mtd", EntityMtdWithFunctions);
        // No .cs files with partial class TestEntity

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("PartialClass", result);
        Assert.Contains("partial class TestEntity", result);
    }

    [Fact]
    public async Task Check_PartialClass_Present_NoIssue()
    {
        var pkg = CreatePackageDir("pkg_partial_ok");
        WriteFile(pkg, "CleanEntity.mtd", CleanEntityMtd);
        WriteFile(pkg, "Server/CleanEntity.cs", """
            namespace MyModule.Server
            {
                public partial class CleanEntity { }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.DoesNotContain("PartialClass", result);
    }

    #endregion

    #region Check 5 — Namespace Consistency

    [Fact]
    public async Task Check_Namespace_WrongSuffix_ReportsIssue()
    {
        var pkg = CreatePackageDir("pkg_namespace");
        WriteFile(pkg, "CleanEntity.mtd", CleanEntityMtd);
        WriteFile(pkg, "Server/Functions.cs", """
            namespace MyModule.Client
            {
                public partial class CleanEntity { }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("Namespace", result);
        Assert.Contains(".Server", result);
    }

    [Fact]
    public async Task Check_Namespace_CorrectSuffix_NoIssue()
    {
        var pkg = CreatePackageDir("pkg_namespace_ok");
        WriteFile(pkg, "CleanEntity.mtd", CleanEntityMtd);
        WriteFile(pkg, "Server/Functions.cs", """
            namespace MyModule.Server
            {
                public partial class CleanEntity { }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.DoesNotContain("Namespace", result);
    }

    #endregion

    #region Check 6 — ModuleInitializer

    [Fact]
    public async Task Check_ModuleInitializer_Missing_ReportsIssue()
    {
        var pkg = CreatePackageDir("pkg_mod_init");
        WriteFile(pkg, "Module.mtd", ModuleMtdWithCoverAction);
        // No ModuleInitializer.cs and no class inheriting ModuleInitializer

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("ModuleInitializer", result);
    }

    [Fact]
    public async Task Check_ModuleInitializer_Present_NoIssue()
    {
        var pkg = CreatePackageDir("pkg_mod_init_ok");
        WriteFile(pkg, "Module.mtd", ModuleMtdWithCoverAction);
        WriteFile(pkg, "ModuleInitializer.cs", """
            namespace MyModule.Server
            {
                public class Initializer : ModuleInitializer { }
            }
            """);
        // Also add a client function to avoid that issue
        WriteFile(pkg, "ModuleClientFunctions.cs", """
            namespace MyModule.Client
            {
                public partial class Module
                {
                    public void ShowDashboardDialog() { }
                }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.DoesNotContain("ModuleInitializer", result);
    }

    #endregion

    #region Clean Package

    [Fact]
    public async Task Check_CleanPackage_AllChecksPassed()
    {
        var pkg = CreatePackageDir("pkg_clean");
        WriteFile(pkg, "CleanEntity.mtd", CleanEntityMtd);
        WriteFile(pkg, "Server/CleanEntity.cs", """
            namespace MyModule.Server
            {
                public partial class CleanEntity { }
            }
            """);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("Все проверки пройдены", result);
        Assert.Contains("Проблем найдено: 0", result);
    }

    [Fact]
    public async Task Check_Report_ContainsSummary()
    {
        var pkg = CreatePackageDir("pkg_summary");
        WriteFile(pkg, "CleanEntity.mtd", CleanEntityMtd);

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("Итого", result);
        Assert.Contains("Проверено сущностей:", result);
        Assert.Contains("Проверено .cs файлов:", result);
        Assert.Contains("Проблем найдено:", result);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Check_NonexistentPath_ReturnsError()
    {
        var result = await _tool.CheckCodeConsistency(Path.Combine(_tempDir, "no_such"));

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найдена", result);
    }

    [Fact]
    public async Task Check_NoMtdFiles_ReturnsError()
    {
        var pkg = CreatePackageDir("pkg_empty");

        var result = await _tool.CheckCodeConsistency(pkg);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains(".mtd", result);
    }

    [Fact]
    public async Task Check_PathOutsideSolutionPath_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr";
        if (!Directory.Exists(outsidePath))
            return;

        var result = await _tool.CheckCodeConsistency(outsidePath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion
}
