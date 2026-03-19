using System.ComponentModel;
using System.IO.Compression;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class BuildDatTool
{
    [McpServerTool(Name = "build_dat")]
    [Description("Собрать .dat пакет из директории. Используй после scaffold/fix/sync.")]
    public async Task<string> BuildDat(
        [Description("Путь к директории пакета (должна содержать source/ и/или settings/)")]
        string packagePath,
        [Description("Путь для сохранения .dat файла. По умолчанию: родительская директория packagePath, имя файла = имя директории + \".dat\"")]
        string? outputPath = null,
        [Description("Строка версии для PackageInfo.xml (например, \"1.0.0.0\"). Если не указана — читается из Module.mtd, если доступен.")]
        string? version = null)
    {
        // Normalize path
        packagePath = Path.GetFullPath(packagePath);

        // Validate packagePath is allowed
        if (!PathGuard.IsAllowed(packagePath))
            return PathGuard.DenyMessage(packagePath);

        // Validate packagePath exists
        if (!Directory.Exists(packagePath))
            return $"**ОШИБКА**: Директория пакета не найдена: `{packagePath}`";

        // Validate source/ or settings/ exists
        string sourceDir = Path.Combine(packagePath, "source");
        string settingsDir = Path.Combine(packagePath, "settings");
        bool hasSource = Directory.Exists(sourceDir);
        bool hasSettings = Directory.Exists(settingsDir);

        if (!hasSource && !hasSettings)
            return $"**ОШИБКА**: Директория пакета не содержит ни `source/`, ни `settings/`: `{packagePath}`";

        // Determine outputPath
        string directoryName = Path.GetFileName(packagePath);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string? parentDir = Path.GetDirectoryName(packagePath);
            if (string.IsNullOrWhiteSpace(parentDir))
                return $"**ОШИБКА**: Не удалось определить родительскую директорию для `{packagePath}`";
            outputPath = Path.Combine(parentDir, directoryName + ".dat");
        }
        else
        {
            outputPath = Path.GetFullPath(outputPath);
        }

        // Validate outputPath is allowed
        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        // Resolve version
        string resolvedVersion = version ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedVersion))
        {
            resolvedVersion = TryReadVersionFromMtd(packagePath) ?? "1.0.0.0";
        }

        // Find or generate PackageInfo.xml content
        string packageInfoPath = Path.Combine(packagePath, "PackageInfo.xml");
        bool packageInfoExisted = File.Exists(packageInfoPath);
        string packageInfoContent;

        if (packageInfoExisted)
        {
            packageInfoContent = await File.ReadAllTextAsync(packageInfoPath);
            // Try to extract version from existing PackageInfo.xml for reporting
            try
            {
                var doc = XDocument.Parse(packageInfoContent);
                string? xmlVersion = doc.Root?.Element("Version")?.Value;
                if (!string.IsNullOrWhiteSpace(xmlVersion))
                    resolvedVersion = xmlVersion;
            }
            catch
            {
                // ignore parse errors, use what we have
            }
        }
        else
        {
            packageInfoContent = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <PackageInfo>
                  <Name>{SecurityEncodeXml(directoryName)}</Name>
                  <Version>{SecurityEncodeXml(resolvedVersion)}</Version>
                  <ExportDate>{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}</ExportDate>
                </PackageInfo>
                """;
        }

        // Collect files
        var sourceFiles = hasSource
            ? Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        var settingsFiles = hasSettings
            ? Directory.GetFiles(settingsDir, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();

        int totalFiles = sourceFiles.Length + settingsFiles.Length + 1; // +1 for PackageInfo.xml

        // Ensure output directory exists
        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        // Delete existing .dat if present
        if (File.Exists(outputPath))
            File.Delete(outputPath);

        // Build ZIP archive
        await Task.Run(() =>
        {
            using var zipStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

            // Add source/ files
            foreach (string filePath in sourceFiles)
            {
                string entryName = GetRelativeEntryName(packagePath, filePath);
                archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
            }

            // Add settings/ files
            foreach (string filePath in settingsFiles)
            {
                string entryName = GetRelativeEntryName(packagePath, filePath);
                archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
            }

            // Add PackageInfo.xml
            var packageInfoEntry = archive.CreateEntry("PackageInfo.xml", CompressionLevel.Optimal);
            using var entryStream = packageInfoEntry.Open();
            using var writer = new StreamWriter(entryStream, System.Text.Encoding.UTF8);
            writer.Write(packageInfoContent);
        });

        // Get output file size
        long fileSizeBytes = new FileInfo(outputPath).Length;
        string fileSizeDisplay = FormatFileSize(fileSizeBytes);

        // Build report
        var report = $"""
            ## Пакет собран

            **Путь:** `{outputPath}`
            **Размер:** {fileSizeDisplay}
            **Файлов:** {totalFiles}

            ### Состав
            {(hasSource ? $"- source/: {sourceFiles.Length} файлов" : "")}
            {(hasSettings ? $"- settings/: {settingsFiles.Length} файлов" : "")}
            - PackageInfo.xml{(packageInfoExisted ? " (существующий)" : " (сгенерирован)")}

            ### Версия
            {resolvedVersion}
            """;

        return report.Trim();
    }

    private static string? TryReadVersionFromMtd(string packagePath)
    {
        try
        {
            // Look for *.mtd files (Module.mtd) in source/ recursively
            string sourceDir = Path.Combine(packagePath, "source");
            if (!Directory.Exists(sourceDir))
                return null;

            var mtdFiles = Directory.GetFiles(sourceDir, "*.mtd", SearchOption.AllDirectories);
            foreach (string mtdFile in mtdFiles)
            {
                string content = File.ReadAllText(mtdFile);
                var doc = XDocument.Parse(content);
                string? ver = doc.Root?.Element("Version")?.Value
                    ?? doc.Root?.Attribute("Version")?.Value;
                if (!string.IsNullOrWhiteSpace(ver))
                    return ver;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string GetRelativeEntryName(string basePath, string filePath)
    {
        string relative = Path.GetRelativePath(basePath, filePath);
        // Normalize to forward slashes for ZIP compatibility
        return relative.Replace('\\', '/');
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes >= 1024 * 1024)
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        if (bytes >= 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes} байт";
    }

    private static string SecurityEncodeXml(string value)
    {
        return value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
