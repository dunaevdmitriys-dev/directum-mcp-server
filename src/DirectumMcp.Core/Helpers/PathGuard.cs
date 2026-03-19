namespace DirectumMcp.Core.Helpers;

public static class PathGuard
{
    public static bool IsAllowed(string path)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return false;

        var fullPath = Path.GetFullPath(path);
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

    public static string DenyMessage(string path) =>
        $"**ОШИБКА**: Доступ запрещён. Путь `{path}` находится за пределами разрешённых директорий.";
}
