using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class CheckPermissionsToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly CheckPermissionsTool _tool;

    private const string KnownRoleGuid = "11111111-1111-1111-1111-111111111111";
    private const string UnknownRoleGuid = "99999999-9999-9999-9999-999999999999";
    private const string BaseGuid = "04581d26-0780-4cfd-b3cd-c2cafc5798b0";

    public CheckPermissionsToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CheckPermissionsTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new CheckPermissionsTool();
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

    private static void WriteFile(string dir, string relativePath, string content)
    {
        var fullPath = Path.Combine(dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static string BuildEntityMtd(string entityName, string accessRightsJson)
    {
        return
            "{\n" +
            "  \"$type\": \"Sungero.Metadata.EntityMetadata\",\n" +
            "  \"NameGuid\": \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\",\n" +
            "  \"Name\": \"" + entityName + "\",\n" +
            "  \"BaseGuid\": \"" + BaseGuid + "\",\n" +
            "  \"Properties\": [],\n" +
            "  \"AccessRights\": " + accessRightsJson + "\n" +
            "}";
    }

    private static string BuildModuleMtd(string rolesJson)
    {
        return
            "{\n" +
            "  \"$type\": \"Sungero.Metadata.ModuleMetadata\",\n" +
            "  \"NameGuid\": \"bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb\",\n" +
            "  \"Name\": \"TestModule\",\n" +
            "  \"Version\": \"1.0.0.0\",\n" +
            "  \"Roles\": " + rolesJson + "\n" +
            "}";
    }

    private static string BuildAccessRight(string roleGuid, string rightType, bool isGranted = true)
    {
        return "{ \"RoleGuid\": \"" + roleGuid + "\", \"AccessRightType\": \"" + rightType + "\", \"IsGranted\": " + (isGranted ? "true" : "false") + " }";
    }

    private static string BuildRoleEntry(string nameGuid, string name)
    {
        return "{ \"NameGuid\": \"" + nameGuid + "\", \"Name\": \"" + name + "\" }";
    }

    #endregion

    #region Test 1 — Корректные AccessRights: нет ошибок

    [Fact]
    public async Task ValidAccessRights_NoIssuesReported()
    {
        var pkg = CreatePackageDir("pkg_valid");

        var ar = "[" + BuildAccessRight(KnownRoleGuid, "Read") + ", " + BuildAccessRight(KnownRoleGuid, "Update") + "]";
        WriteFile(pkg, "MyEntity.mtd", BuildEntityMtd("MyEntity", ar));
        WriteFile(pkg, "Module.mtd", BuildModuleMtd("[" + BuildRoleEntry(KnownRoleGuid, "TestRole") + "]"));

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Проблем не обнаружено", result);
        Assert.DoesNotContain("Error]", result);
        Assert.DoesNotContain("Warning]", result);
    }

    #endregion

    #region Test 2 — Пустые AccessRights: Warning

    [Fact]
    public async Task EmptyAccessRights_ReturnsWarning()
    {
        var pkg = CreatePackageDir("pkg_empty_ar");
        WriteFile(pkg, "MyEntity.mtd", BuildEntityMtd("MyEntity", "[]"));

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Warning", result);
        Assert.Contains("EmptyAccessRights", result);
        Assert.DoesNotContain("Проблем не обнаружено", result);
    }

    [Fact]
    public async Task MissingAccessRightsBlock_ReturnsWarning()
    {
        var pkg = CreatePackageDir("pkg_no_ar");
        WriteFile(pkg, "MyEntity.mtd",
            "{\n" +
            "  \"$type\": \"Sungero.Metadata.EntityMetadata\",\n" +
            "  \"NameGuid\": \"a1b2c3d4-e5f6-7890-abcd-ef1234567890\",\n" +
            "  \"Name\": \"NoArEntity\",\n" +
            "  \"BaseGuid\": \"" + BaseGuid + "\",\n" +
            "  \"Properties\": []\n" +
            "}");

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Warning", result);
        Assert.Contains("EmptyAccessRights", result);
    }

    #endregion

    #region Test 3 — Дублирующиеся права: Error

    [Fact]
    public async Task DuplicateAccessRights_ReturnsError()
    {
        var pkg = CreatePackageDir("pkg_duplicates");
        var right = BuildAccessRight(KnownRoleGuid, "Read");
        var ar = "[" + right + ", " + right + "]";
        WriteFile(pkg, "MyEntity.mtd", BuildEntityMtd("MyEntity", ar));

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Error", result);
        Assert.Contains("DuplicateRight", result);
    }

    #endregion

    #region Test 4 — Неизвестный AccessRightType: Warning

    [Fact]
    public async Task UnknownAccessRightType_ReturnsWarning()
    {
        var pkg = CreatePackageDir("pkg_unknown_type");
        var ar = "[" + BuildAccessRight(KnownRoleGuid, "Execute") + "]";
        WriteFile(pkg, "MyEntity.mtd", BuildEntityMtd("MyEntity", ar));
        // No Module.mtd — role check skipped, but type check still runs

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Warning", result);
        Assert.Contains("UnknownRightType", result);
        Assert.Contains("Execute", result);
    }

    #endregion

    #region Test 5 — Несуществующий путь: ошибка

    [Fact]
    public async Task NonexistentPath_ReturnsError()
    {
        var nonexistent = Path.Combine(_tempDir, "does_not_exist");

        var result = await _tool.CheckPermissions(nonexistent);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найден", result);
    }

    #endregion

    #region Test 6 — Роль не найдена в Module.mtd: Warning

    [Fact]
    public async Task RoleNotInModuleMtd_ReturnsWarning()
    {
        var pkg = CreatePackageDir("pkg_unknown_role");
        var ar = "[" + BuildAccessRight(UnknownRoleGuid, "Read") + "]";
        WriteFile(pkg, "MyEntity.mtd", BuildEntityMtd("MyEntity", ar));
        // Module.mtd with a different role — UnknownRoleGuid is not declared
        WriteFile(pkg, "Module.mtd", BuildModuleMtd("[" + BuildRoleEntry(KnownRoleGuid, "KnownRole") + "]"));

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Warning", result);
        Assert.Contains("UnknownRoleGuid", result);
        Assert.Contains(UnknownRoleGuid, result);
    }

    #endregion

    #region Test 7 — Несколько сущностей в пакете

    [Fact]
    public async Task MultipleEntitiesInPackage_EachReportedSeparately()
    {
        var pkg = CreatePackageDir("pkg_multi");

        // Entity1: correct rights — should NOT appear in report
        var ar1 = "[" + BuildAccessRight(KnownRoleGuid, "Read") + "]";
        WriteFile(pkg, "Entity1.mtd",
            "{\n" +
            "  \"$type\": \"Sungero.Metadata.EntityMetadata\",\n" +
            "  \"NameGuid\": \"a1111111-1111-1111-1111-111111111111\",\n" +
            "  \"Name\": \"Entity1\",\n" +
            "  \"BaseGuid\": \"" + BaseGuid + "\",\n" +
            "  \"Properties\": [],\n" +
            "  \"AccessRights\": " + ar1 + "\n" +
            "}");

        // Entity2: empty AccessRights — should produce Warning
        WriteFile(pkg, "Entity2.mtd",
            "{\n" +
            "  \"$type\": \"Sungero.Metadata.EntityMetadata\",\n" +
            "  \"NameGuid\": \"a2222222-2222-2222-2222-222222222222\",\n" +
            "  \"Name\": \"Entity2\",\n" +
            "  \"BaseGuid\": \"" + BaseGuid + "\",\n" +
            "  \"Properties\": [],\n" +
            "  \"AccessRights\": []\n" +
            "}");

        // Entity3: duplicate rights — should produce Error
        var dupRight = BuildAccessRight(KnownRoleGuid, "Delete");
        WriteFile(pkg, "Entity3.mtd",
            "{\n" +
            "  \"$type\": \"Sungero.Metadata.EntityMetadata\",\n" +
            "  \"NameGuid\": \"a3333333-3333-3333-3333-333333333333\",\n" +
            "  \"Name\": \"Entity3\",\n" +
            "  \"BaseGuid\": \"" + BaseGuid + "\",\n" +
            "  \"Properties\": [],\n" +
            "  \"AccessRights\": [" + dupRight + ", " + dupRight + "]\n" +
            "}");

        WriteFile(pkg, "Module.mtd", BuildModuleMtd("[" + BuildRoleEntry(KnownRoleGuid, "TestRole") + "]"));

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Entity2", result);
        Assert.Contains("Entity3", result);
        Assert.Contains("EmptyAccessRights", result);
        Assert.Contains("DuplicateRight", result);
        // Entity1 should NOT appear in the report (no issues)
        Assert.DoesNotContain("## Entity1", result);
    }

    #endregion

    #region Test 8 — Путь за пределами SOLUTION_PATH: ошибка доступа

    [Fact]
    public async Task PathOutsideSolutionPath_ReturnsDenied()
    {
        // Use a non-temp path that is guaranteed to be outside SOLUTION_PATH (_tempDir)
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
        {
            // Skip test if we can't find a suitable outside path
            return;
        }

        var result = await _tool.CheckPermissions(outsidePath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion

    #region Test 9 — Единственный .mtd файл как аргумент

    [Fact]
    public async Task SingleMtdFile_ValidPath_Works()
    {
        var pkg = CreatePackageDir("pkg_single_file");
        var ar = "[" + BuildAccessRight(KnownRoleGuid, "Read") + "]";
        var mtdPath = Path.Combine(pkg, "MyEntity.mtd");
        File.WriteAllText(mtdPath, BuildEntityMtd("MyEntity", ar));

        var result = await _tool.CheckPermissions(mtdPath);

        // No Module.mtd found — role check skipped but report should still run
        Assert.Contains("MTD файлов проверено", result);
    }

    #endregion

    #region Test 10 — Все известные типы прав валидны

    [Fact]
    public async Task AllKnownAccessRightTypes_NoWarning()
    {
        var pkg = CreatePackageDir("pkg_all_types");
        var knownTypes = new[] { "Read", "Update", "Create", "Delete", "Approve", "SendByExchange", "Register", "ChangeAccess" };
        var rights = string.Join(", ", knownTypes.Select(t => BuildAccessRight(KnownRoleGuid, t)));
        var ar = "[" + rights + "]";

        WriteFile(pkg, "MyEntity.mtd", BuildEntityMtd("MyEntity", ar));
        WriteFile(pkg, "Module.mtd", BuildModuleMtd("[" + BuildRoleEntry(KnownRoleGuid, "TestRole") + "]"));

        var result = await _tool.CheckPermissions(pkg);

        Assert.Contains("Проблем не обнаружено", result);
        Assert.DoesNotContain("UnknownRightType", result);
    }

    #endregion
}
