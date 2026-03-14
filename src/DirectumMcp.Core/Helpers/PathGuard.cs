namespace DirectumMcp.Core.Helpers;

public static class PathGuard
{
    public static bool IsAllowed(string path)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return false;

        var fullPath = Path.GetFullPath(path);
        var allowedPaths = new[]
        {
            Path.GetFullPath(solutionPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        };
        return allowedPaths.Any(bp =>
            bp.Length >= 4 &&
            (string.Equals(fullPath, bp, StringComparison.OrdinalIgnoreCase) ||
             fullPath.StartsWith(bp + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
             fullPath.StartsWith(bp + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)));
    }

    public static string DenyMessage(string path) =>
        $"**ОШИБКА**: Доступ запрещён. Путь `{path}` находится за пределами разрешённых директорий.";
}
