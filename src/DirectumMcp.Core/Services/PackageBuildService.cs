using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for build_dat. Used by MCP tool and pipeline.
/// </summary>
public class PackageBuildService : IPipelineStep
{
    public string ToolName => "build_dat";

    public async Task<BuildDatResult> BuildAsync(
        string packagePath,
        string? outputPath = null,
        string? version = null,
        CancellationToken ct = default)
    {
        packagePath = Path.GetFullPath(packagePath);

        if (!Directory.Exists(packagePath))
            return Fail($"Директория пакета не найдена: `{packagePath}`");

        string sourceDir = Path.Combine(packagePath, "source");
        string settingsDir = Path.Combine(packagePath, "settings");
        bool hasSource = Directory.Exists(sourceDir);
        bool hasSettings = Directory.Exists(settingsDir);

        if (!hasSource && !hasSettings)
            return Fail($"Директория пакета не содержит ни `source/`, ни `settings/`: `{packagePath}`");

        string directoryName = Path.GetFileName(packagePath);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            string? parentDir = Path.GetDirectoryName(packagePath);
            if (string.IsNullOrWhiteSpace(parentDir))
                return Fail($"Не удалось определить родительскую директорию для `{packagePath}`");
            outputPath = Path.Combine(parentDir, directoryName + ".dat");
        }
        else
        {
            outputPath = Path.GetFullPath(outputPath);
        }

        string resolvedVersion = version ?? string.Empty;
        if (string.IsNullOrWhiteSpace(resolvedVersion))
            resolvedVersion = TryReadVersionFromMtd(packagePath) ?? "1.0.0.0";

        string packageInfoPath = Path.Combine(packagePath, "PackageInfo.xml");
        bool packageInfoExisted = File.Exists(packageInfoPath);
        string packageInfoContent;

        if (packageInfoExisted)
        {
            packageInfoContent = await File.ReadAllTextAsync(packageInfoPath, ct);
            try
            {
                var doc = XDocument.Parse(packageInfoContent);
                string? xmlVersion = doc.Root?.Element("Version")?.Value;
                if (!string.IsNullOrWhiteSpace(xmlVersion))
                    resolvedVersion = xmlVersion;
            }
            catch { }
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

        var sourceFiles = hasSource
            ? Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();
        var settingsFiles = hasSettings
            ? Directory.GetFiles(settingsDir, "*", SearchOption.AllDirectories)
            : Array.Empty<string>();

        int totalFiles = sourceFiles.Length + settingsFiles.Length + 1;

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(outputDir) && !Directory.Exists(outputDir))
            Directory.CreateDirectory(outputDir);

        if (File.Exists(outputPath))
            File.Delete(outputPath);

        await Task.Run(() =>
        {
            using var zipStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false);

            foreach (string filePath in sourceFiles)
            {
                string entryName = Path.GetRelativePath(packagePath, filePath).Replace('\\', '/');
                archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
            }

            foreach (string filePath in settingsFiles)
            {
                string entryName = Path.GetRelativePath(packagePath, filePath).Replace('\\', '/');
                archive.CreateEntryFromFile(filePath, entryName, CompressionLevel.Optimal);
            }

            var packageInfoEntry = archive.CreateEntry("PackageInfo.xml", CompressionLevel.Optimal);
            using var entryStream = packageInfoEntry.Open();
            using var writer = new StreamWriter(entryStream, System.Text.Encoding.UTF8);
            writer.Write(packageInfoContent);
        }, ct);

        long fileSizeBytes = new FileInfo(outputPath).Length;

        return new BuildDatResult
        {
            Success = true,
            OutputPath = outputPath,
            Version = resolvedVersion,
            FileSizeBytes = fileSizeBytes,
            FileCount = totalFiles,
            SourceFileCount = sourceFiles.Length,
            SettingsFileCount = settingsFiles.Length,
            PackageInfoGenerated = !packageInfoExisted
        };
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        var path = GetString(parameters, "packagePath");
        var output = GetStringOrNull(parameters, "outputPath");
        var ver = GetStringOrNull(parameters, "version");
        return await BuildAsync(path, output, ver, ct);
    }

    private static string? TryReadVersionFromMtd(string packagePath)
    {
        try
        {
            string sourceDir = Path.Combine(packagePath, "source");
            if (!Directory.Exists(sourceDir)) return null;

            var mtdFiles = Directory.GetFiles(sourceDir, "*.mtd", SearchOption.AllDirectories);
            foreach (string mtdFile in mtdFiles)
            {
                string content = File.ReadAllText(mtdFile);
                var doc = XDocument.Parse(content);
                string? ver = doc.Root?.Element("Version")?.Value
                    ?? doc.Root?.Attribute("Version")?.Value;
                if (!string.IsNullOrWhiteSpace(ver)) return ver;
            }
        }
        catch { }
        return null;
    }

    private static string SecurityEncodeXml(string value) =>
        value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
             .Replace("\"", "&quot;").Replace("'", "&apos;");

    private static BuildDatResult Fail(string error) =>
        new() { Success = false, Errors = [error] };

    private static string GetString(Dictionary<string, JsonElement> p, string key, string def = "") =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : def;

    private static string? GetStringOrNull(Dictionary<string, JsonElement> p, string key) =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() : null;
}
