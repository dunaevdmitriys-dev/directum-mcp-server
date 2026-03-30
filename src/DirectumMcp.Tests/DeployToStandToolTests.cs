using System.IO.Compression;
using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class DeployToStandToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly string? _previousDeploymentToolPath;
    private readonly string? _previousStagingPath;
    private readonly DeployToStandTool _tool;

    public DeployToStandToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DeployToStandTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        _previousDeploymentToolPath = Environment.GetEnvironmentVariable("DEPLOYMENT_TOOL_PATH");
        _previousStagingPath = Environment.GetEnvironmentVariable("DEPLOYMENT_STAGING_PATH");

        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        // Clear deployment-specific vars so tests are deterministic
        Environment.SetEnvironmentVariable("DEPLOYMENT_TOOL_PATH", null);
        Environment.SetEnvironmentVariable("DEPLOYMENT_STAGING_PATH", null);

        _tool = new DeployToStandTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        Environment.SetEnvironmentVariable("DEPLOYMENT_TOOL_PATH", _previousDeploymentToolPath);
        Environment.SetEnvironmentVariable("DEPLOYMENT_STAGING_PATH", _previousStagingPath);

        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Creates a minimal valid .dat file (ZIP with PackageInfo.xml).</summary>
    private string CreateValidDat(string name = "TestPackage", string version = "1.0.0.0")
    {
        var datPath = Path.Combine(_tempDir, name + ".dat");
        using var stream = new FileStream(datPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        var entry = archive.CreateEntry("PackageInfo.xml");
        using var writer = new StreamWriter(entry.Open());
        writer.Write($"""
            <?xml version="1.0" encoding="utf-8"?>
            <PackageInfo>
              <Name>{name}</Name>
              <Version>{version}</Version>
            </PackageInfo>
            """);

        return datPath;
    }

    /// <summary>Creates a .dat that is a valid ZIP but has no PackageInfo.xml.</summary>
    private string CreateDatWithoutPackageInfo(string name = "NoInfo")
    {
        var datPath = Path.Combine(_tempDir, name + ".dat");
        using var stream = new FileStream(datPath, FileMode.Create, FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create);

        var entry = archive.CreateEntry("source/SomeEntity.mtd");
        using var writer = new StreamWriter(entry.Open());
        writer.Write("{}");

        return datPath;
    }

    /// <summary>Creates a file that is NOT a valid ZIP (just random bytes).</summary>
    private string CreateInvalidZip(string name = "Corrupt")
    {
        var datPath = Path.Combine(_tempDir, name + ".dat");
        File.WriteAllBytes(datPath, new byte[] { 0x00, 0x01, 0x02, 0x03, 0xFF, 0xFE });
        return datPath;
    }

    // ── Tests ─────────────────────────────────────────────────────────────────

    /// <summary>Test 1: Несуществующий .dat → ошибка.</summary>
    [Fact]
    public async Task NonexistentDat_ReturnsError()
    {
        var nonExistentPath = Path.Combine(_tempDir, "ghost.dat");

        var result = await _tool.Deploy(nonExistentPath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найден", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Test 2: Валидный .dat в dry-run → план без выполнения.</summary>
    [Fact]
    public async Task ValidDat_DryRun_ReturnsPlanWithoutExecution()
    {
        var datPath = CreateValidDat("MyModule", "2.5.0.0");

        var result = await _tool.Deploy(datPath, confirm: false, dry_run: true);

        // Must contain plan header
        Assert.Contains("# План деплоя", result);
        // Must contain the dat path
        Assert.Contains(datPath, result);
        // Must show PackageInfo details
        Assert.Contains("MyModule", result);
        Assert.Contains("2.5.0.0", result);
        // Must indicate dry-run mode
        Assert.Contains("DRY-RUN", result, StringComparison.OrdinalIgnoreCase);
        // Must NOT claim any real action was performed
        Assert.DoesNotContain("скопирован в", result);
    }

    /// <summary>Test 3: confirm=false → только plan (dry-run), независимо от dry_run флага.</summary>
    [Fact]
    public async Task ConfirmFalse_AlwaysReturnsPlanOnly()
    {
        var datPath = CreateValidDat("PkgConfirmFalse");

        // Even with dry_run=false, confirm=false must force dry-run
        var result = await _tool.Deploy(datPath, confirm: false, dry_run: false);

        Assert.Contains("# План деплоя", result);
        Assert.Contains("DRY-RUN", result, StringComparison.OrdinalIgnoreCase);
        // No copy should have happened
        Assert.DoesNotContain("скопирован", result);
    }

    /// <summary>Test 4: Путь за пределами SOLUTION_PATH → ошибка доступа.</summary>
    [Fact]
    public async Task PathOutsideSolutionPath_ReturnsAccessDenied()
    {
        // Use a path that is definitely outside _tempDir (which is SOLUTION_PATH)
        var outsidePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "outside_solution.dat");

        var result = await _tool.Deploy(outsidePath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Test 5: Невалидный ZIP → ошибка.</summary>
    [Fact]
    public async Task InvalidZip_ReturnsError()
    {
        var invalidPath = CreateInvalidZip("BadZip");

        var result = await _tool.Deploy(invalidPath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("ZIP", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Test 6: .dat без PackageInfo.xml → предупреждение, но план показывается.</summary>
    [Fact]
    public async Task DatWithoutPackageInfo_ShowsWarningButContinues()
    {
        var datPath = CreateDatWithoutPackageInfo("NoPkgInfo");

        var result = await _tool.Deploy(datPath, confirm: false, dry_run: true);

        // Should not be a hard error — plan must still be shown
        Assert.Contains("# План деплоя", result);
        // Must indicate that PackageInfo is missing (warning)
        Assert.Contains("PackageInfo.xml", result);
        Assert.Contains("Предупреждение", result, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Bonus: plan always contains all 5 numbered steps.</summary>
    [Fact]
    public async Task DryRun_PlanContainsAllFiveSteps()
    {
        var datPath = CreateValidDat("StepsPkg");

        var result = await _tool.Deploy(datPath, confirm: false, dry_run: true);

        Assert.Contains("1.", result);
        Assert.Contains("2.", result);
        Assert.Contains("3.", result);
        Assert.Contains("4.", result);
        Assert.Contains("5.", result);
        // Must contain PowerShell commands for service management
        Assert.Contains("Stop-WebAppPool", result);
        Assert.Contains("Stop-Service", result);
        Assert.Contains("Start-WebAppPool", result);
        Assert.Contains("Start-Service", result);
        // Must contain DeploymentTool invocation
        Assert.Contains("DeploymentTool.exe", result);
    }

    /// <summary>Bonus: confirm=true + dry_run=false + DEPLOYMENT_STAGING_PATH set → copies file to staging.</summary>
    [Fact]
    public async Task ConfirmTrue_WithStagingPath_CopiesFile()
    {
        var datPath = CreateValidDat("StagingPkg");
        var stagingDir = Path.Combine(_tempDir, "staging");
        Directory.CreateDirectory(stagingDir);
        Environment.SetEnvironmentVariable("DEPLOYMENT_STAGING_PATH", stagingDir);

        var result = await _tool.Deploy(datPath, confirm: true, dry_run: false);

        // File should have been copied
        var expectedCopied = Path.Combine(stagingDir, "StagingPkg.dat");
        Assert.True(File.Exists(expectedCopied), $"Expected copied file at {expectedCopied}");
        Assert.Contains("✅", result);
        Assert.Contains("скопирован", result, StringComparison.OrdinalIgnoreCase);
    }
}
