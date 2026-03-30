using DirectumMcp.Core.Helpers;
using Xunit;

namespace DirectumMcp.Tests;

public class PathGuardTraversalTests
{
    [Theory]
    [InlineData("../etc/passwd", true)]
    [InlineData("..\\windows\\system32", true)]
    [InlineData("some/../../etc/passwd", true)]
    [InlineData("..", true)]
    [InlineData("normal/path/file.txt", false)]
    [InlineData("C:\\valid\\path", false)]
    [InlineData("relative/file.mtd", false)]
    public void ContainsTraversal_Scenarios(string path, bool expected)
    {
        Assert.Equal(expected, PathGuard.ContainsTraversal(path));
    }

    [Fact]
    public void ContainsTraversal_NullByte_Detected()
    {
        Assert.True(PathGuard.ContainsTraversal("file\0.txt"));
    }

    [Fact]
    public void ContainsTraversal_ControlChars_Detected()
    {
        Assert.True(PathGuard.ContainsTraversal("file\x01name.txt"));
    }

    [Fact]
    public void ContainsTraversal_EmptyString_ReturnsFalse()
    {
        Assert.False(PathGuard.ContainsTraversal(""));
    }

    [Fact]
    public void IsAllowed_TraversalPath_ReturnsFalse()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pg_test_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", tempDir);
            // Path that uses ".." to escape
            Assert.False(PathGuard.IsAllowed(Path.Combine(tempDir, "..", "escaped")));
        }
        finally
        {
            Directory.Delete(tempDir);
            Environment.SetEnvironmentVariable("SOLUTION_PATH", null);
        }
    }

    [Fact]
    public void IsAllowed_NullPath_ReturnsFalse()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", "C:\\valid");
        Assert.False(PathGuard.IsAllowed(""));
        Assert.False(PathGuard.IsAllowed("   "));
    }

    [Fact]
    public void ValidateAndNormalize_ValidPath_ReturnsNormalized()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "pg_norm_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);
        try
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", tempDir);
            var result = PathGuard.ValidateAndNormalize(Path.Combine(tempDir, "sub", "file.mtd"));
            Assert.NotNull(result);
            Assert.StartsWith(Path.GetFullPath(tempDir), result);
        }
        finally
        {
            Directory.Delete(tempDir);
            Environment.SetEnvironmentVariable("SOLUTION_PATH", null);
        }
    }

    [Fact]
    public void ValidateAndNormalize_TraversalPath_ReturnsNull()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", "C:\\valid\\path");
        var result = PathGuard.ValidateAndNormalize("C:\\valid\\path\\..\\..\\etc");
        Assert.Null(result);
        Environment.SetEnvironmentVariable("SOLUTION_PATH", null);
    }
}
