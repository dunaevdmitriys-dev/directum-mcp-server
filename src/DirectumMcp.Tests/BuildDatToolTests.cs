using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class BuildDatToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly BuildDatTool _tool;

    public BuildDatToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "BuildDatTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new BuildDatTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreatePackage(string name)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(Path.Combine(dir, "source"));
        Directory.CreateDirectory(Path.Combine(dir, "settings"));
        File.WriteAllText(Path.Combine(dir, "source", "Entity.mtd"), "{}");
        File.WriteAllText(Path.Combine(dir, "settings", "config.xml"), "<config/>");
        return dir;
    }

    [Fact]
    public async Task Build_ValidPackage_CreatesDatFile()
    {
        var pkg = CreatePackage("pkg_valid");
        var datPath = Path.Combine(_tempDir, "pkg_valid.dat");

        var result = await _tool.BuildDat(pkg, datPath);

        Assert.Contains("собран", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(datPath));
    }

    [Fact]
    public async Task Build_ValidPackage_ReportContainsFileCount()
    {
        var pkg = CreatePackage("pkg_count");
        var datPath = Path.Combine(_tempDir, "pkg_count.dat");

        var result = await _tool.BuildDat(pkg, datPath);

        Assert.Contains("source", result);
        Assert.Contains("settings", result);
    }

    [Fact]
    public async Task Build_WithExistingPackageInfo_IncludesIt()
    {
        var pkg = CreatePackage("pkg_pkginfo");
        File.WriteAllText(Path.Combine(pkg, "PackageInfo.xml"),
            "<PackageInfo><Name>Test</Name><Version>2.0.0.0</Version></PackageInfo>");
        var datPath = Path.Combine(_tempDir, "pkg_pkginfo.dat");

        var result = await _tool.BuildDat(pkg, datPath);

        Assert.True(File.Exists(datPath));
    }

    [Fact]
    public async Task Build_NoSourceOrSettings_ReturnsError()
    {
        var emptyDir = Path.Combine(_tempDir, "pkg_empty");
        Directory.CreateDirectory(emptyDir);

        var result = await _tool.BuildDat(emptyDir);

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Build_NonexistentPath_ReturnsError()
    {
        var result = await _tool.BuildDat(Path.Combine(_tempDir, "no_such"));

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Build_PathOutsideSolution_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            return;

        var result = await _tool.BuildDat(outsidePath);

        Assert.Contains("ОШИБКА", result);
    }
}
