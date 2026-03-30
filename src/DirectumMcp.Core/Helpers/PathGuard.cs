namespace DirectumMcp.Core.Helpers;

public static class PathGuard
{
    public static bool IsAllowed(string path)
    {
        // Reject null/empty
        if (string.IsNullOrWhiteSpace(path))
            return false;

        // Normalize and check for traversal attempts BEFORE GetFullPath
        // GetFullPath resolves ".." which could bypass checks
        if (ContainsTraversal(path))
            return false;

        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return false;

        var fullPath = Path.GetFullPath(path);

        // Double-check: normalized path should not differ in directory depth
        // (GetFullPath resolves symlinks/junctions on some OS)
        if (ContainsTraversal(fullPath))
            return false;

        var allowed = new List<string>
        {
            Path.GetFullPath(solutionPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        };

        // Allow WORKSPACE_PATH (project working directory, e.g. Downloads\Директум)
        var workspacePath = Environment.GetEnvironmentVariable("WORKSPACE_PATH");
        if (!string.IsNullOrEmpty(workspacePath))
            allowed.Add(Path.GetFullPath(workspacePath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        // Allow LAUNCHER_PATH (DirectumLauncher directory)
        var launcherPath = Environment.GetEnvironmentVariable("LAUNCHER_PATH");
        if (!string.IsNullOrEmpty(launcherPath))
            allowed.Add(Path.GetFullPath(launcherPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        return allowed.Any(bp =>
            bp.Length >= 4 &&
            (string.Equals(fullPath, bp, StringComparison.OrdinalIgnoreCase) ||
             fullPath.StartsWith(bp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
             fullPath.StartsWith(bp + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Validates path and returns normalized full path, or null if denied.
    /// Preferred over IsAllowed + separate Path.GetFullPath to avoid TOCTOU.
    /// </summary>
    public static string? ValidateAndNormalize(string path)
    {
        if (!IsAllowed(path))
            return null;
        return Path.GetFullPath(path);
    }

    /// <summary>
    /// Detects path traversal patterns in raw path string.
    /// </summary>
    public static bool ContainsTraversal(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Check for ".." segments
        var normalized = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

        // ".." at start, end, or between separators
        if (normalized.Contains(".." + Path.DirectorySeparatorChar) ||
            normalized.Contains(Path.DirectorySeparatorChar + "..") ||
            normalized == ".." ||
            normalized.StartsWith(".."))
            return true;

        // Null bytes (path truncation attack)
        if (path.Contains('\0'))
            return true;

        // Control characters
        if (path.Any(c => char.IsControl(c) && c != '\t'))
            return true;

        return false;
    }

    public static string DenyMessage(string path) =>
        $"**ОШИБКА**: Доступ запрещён. Путь `{path}` находится за пределами разрешённых директорий.";
}
