using System.IO.Compression;
using System.Text.Json;

namespace DirectumMcp.Core.Validators;

/// <summary>
/// Encapsulates unpacking a .dat package (or opening a directory),
/// parsing MTD and resx files, and cleaning up temp directories.
/// </summary>
public sealed class PackageWorkspace : IDisposable
{
    public string WorkDir { get; }
    public List<(string Path, JsonDocument Doc)> Entities { get; } = [];
    public List<(string Path, JsonDocument Doc)> Modules { get; } = [];
    public string[] ResxFiles { get; }
    public string[] MtdFiles { get; }
    public bool IsTempDir { get; }
    public bool IsDatFile { get; }

    private PackageWorkspace(string workDir, bool isTempDir, bool isDatFile,
        string[] mtdFiles, string[] resxFiles)
    {
        WorkDir = workDir;
        IsTempDir = isTempDir;
        IsDatFile = isDatFile;
        MtdFiles = mtdFiles;
        ResxFiles = resxFiles;
    }

    /// <summary>
    /// Opens a package from a .dat file or an unpacked directory.
    /// Returns null and sets errorMessage if the path is invalid.
    /// </summary>
    public static async Task<(PackageWorkspace? Workspace, string? ErrorMessage)> OpenAsync(
        string packagePath, bool includeRuResx = false, CancellationToken ct = default)
    {
        string workDir;
        bool isTempDir = false;
        bool isDatFile = false;

        if (File.Exists(packagePath) && packagePath.EndsWith(".dat", StringComparison.OrdinalIgnoreCase))
        {
            workDir = Path.Combine(Path.GetTempPath(), "drx_pkg_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(workDir);
            isTempDir = true;
            isDatFile = true;

            using var archive = ZipFile.OpenRead(packagePath);
            var workDirNorm = Path.GetFullPath(workDir).TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            foreach (var entry in archive.Entries)
            {
                // Zip Slip protection: reject entries with traversal patterns
                if (entry.FullName.Contains("..") ||
                    entry.FullName.Contains('\0') ||
                    Path.IsPathRooted(entry.FullName))
                    throw new InvalidOperationException(
                        $"Zip entry '{entry.FullName}' contains path traversal or absolute path — rejected.");

                var destPath = Path.GetFullPath(Path.Combine(workDir, entry.FullName));

                // Double-check: normalized destination must be within workDir
                if (!destPath.StartsWith(workDirNorm + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !destPath.StartsWith(workDirNorm + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(destPath, workDirNorm, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Zip entry '{entry.FullName}' would extract outside target directory.");

                var dir = Path.GetDirectoryName(destPath);
                if (dir != null) Directory.CreateDirectory(dir);
                if (!string.IsNullOrEmpty(entry.Name))
                    entry.ExtractToFile(destPath, overwrite: true);
            }
        }
        else if (Directory.Exists(packagePath))
        {
            workDir = packagePath;
        }
        else
        {
            return (null, $"**ОШИБКА**: Путь не найден: `{packagePath}`\nУкажите путь к .dat файлу или распакованной директории.");
        }

        var mtdFiles = Directory.GetFiles(workDir, "*.mtd", SearchOption.AllDirectories);

        string[] resxFiles;
        if (includeRuResx)
        {
            resxFiles = Directory.GetFiles(workDir, "*System.resx", SearchOption.AllDirectories)
                .Concat(Directory.GetFiles(workDir, "*System.ru.resx", SearchOption.AllDirectories))
                .Distinct()
                .ToArray();
        }
        else
        {
            resxFiles = Directory.GetFiles(workDir, "*System.resx", SearchOption.AllDirectories);
        }

        var workspace = new PackageWorkspace(workDir, isTempDir, isDatFile, mtdFiles, resxFiles);

        foreach (var f in mtdFiles)
        {
            var json = await File.ReadAllTextAsync(f, ct);
            var doc = JsonDocument.Parse(json);
            var typeProp = doc.RootElement.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
            if (typeProp.Contains("ModuleMetadata"))
                workspace.Modules.Add((f, doc));
            else
                workspace.Entities.Add((f, doc));
        }

        return (workspace, null);
    }

    public void Dispose()
    {
        foreach (var (_, doc) in Entities) doc.Dispose();
        foreach (var (_, doc) in Modules) doc.Dispose();

        if (IsTempDir)
        {
            try { Directory.Delete(WorkDir, true); } catch { /* best effort */ }
        }
    }
}
