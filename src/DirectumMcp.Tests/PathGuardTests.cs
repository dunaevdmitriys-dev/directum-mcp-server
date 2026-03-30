using DirectumMcp.Core.Helpers;
using Xunit;

namespace DirectumMcp.Tests;

public class PathGuardTests : IDisposable
{
    private readonly string? _originalPath;

    public PathGuardTests()
    {
        _originalPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
    }

    public void Dispose()
    {
        if (_originalPath != null)
            Environment.SetEnvironmentVariable("SOLUTION_PATH", _originalPath);
        else
            Environment.SetEnvironmentVariable("SOLUTION_PATH", null);
    }

    [Fact]
    public void IsAllowed_ExactPath_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pathguard_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", tempDir);
            Assert.True(PathGuard.IsAllowed(tempDir));
        }
        finally { Directory.Delete(tempDir); }
    }

    [Fact]
    public void IsAllowed_SubPath_ReturnsTrue()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pathguard_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        try
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", tempDir);
            Assert.True(PathGuard.IsAllowed(Path.Combine(tempDir, "subdir", "file.txt")));
        }
        finally { Directory.Delete(tempDir); }
    }

    [Fact]
    public void IsAllowed_PrefixSibling_ReturnsFalse()
    {
        // Use a non-temp path to test prefix bypass: /opt/myapp vs /opt/myappadmin
        // On Windows use a drive root based path that won't collide with temp
        var baseDir = @"C:\pathguard_unique_" + Guid.NewGuid().ToString("N")[..8];
        Environment.SetEnvironmentVariable("SOLUTION_PATH", baseDir);
        // sibling dir with same prefix + "admin" should NOT be allowed
        Assert.False(PathGuard.IsAllowed(baseDir + "admin"));
    }

    [Fact]
    public void IsAllowed_TempPath_ReturnsTrue()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), "test_file.txt");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", "C:\\some\\valid\\path");
        Assert.True(PathGuard.IsAllowed(tempFile));
    }

    [Fact]
    public void IsAllowed_OutsideBoth_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", "C:\\valid\\solution");
        Assert.False(PathGuard.IsAllowed("D:\\completely\\different\\path"));
    }

    [Fact]
    public void IsAllowed_EmptySolutionPath_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", "");
        Assert.False(PathGuard.IsAllowed("C:\\any\\path"));
    }
}
