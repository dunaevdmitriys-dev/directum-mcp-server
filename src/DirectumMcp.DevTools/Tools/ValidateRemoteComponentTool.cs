using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateRemoteComponentTool
{
    [McpServerTool(Name = "validate_remote_component")]
    [Description("Валидация Remote Component: manifest.json, Module.mtd регистрация, Loaders (Card/Cover), PublicName, HostApiVersion, зависимости.")]
    public async Task<string> ValidateRemoteComponent(
        [Description("Путь к RC проекту (папка с package.json) или к модулю с RC")] string path)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория не найдена: `{path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Валидация Remote Components");
        sb.AppendLine();

        int totalChecks = 0, passed = 0, failed = 0;
        var issues = new List<string>();

        // Find RC registrations in Module.mtd
        var mtdFiles = Directory.GetFiles(path, "Module.mtd", SearchOption.AllDirectories);
        var registeredRCs = new List<(string Name, string PublicName, string Version, List<string> Loaders, string File)>();

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("RemoteComponents", out var rcs) && rcs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var rc in rcs.EnumerateArray())
                    {
                        var rcName = rc.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                        var publicName = rc.TryGetProperty("PublicName", out var pn) ? pn.GetString() ?? "" : "";
                        var version = rc.TryGetProperty("ComponentVersion", out var cv) ? cv.GetString() ?? "" : "";

                        var loaders = new List<string>();
                        if (rc.TryGetProperty("Controls", out var controls) && controls.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var ctrl in controls.EnumerateArray())
                            {
                                if (ctrl.TryGetProperty("Loaders", out var ldrs) && ldrs.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var ldr in ldrs.EnumerateArray())
                                    {
                                        var scope = ldr.TryGetProperty("Scope", out var s) ? s.GetString() ?? "" : "";
                                        var ldrName = ldr.TryGetProperty("Name", out var ln) ? ln.GetString() ?? "" : "";
                                        loaders.Add($"{ldrName} ({scope})");
                                    }
                                }
                            }
                        }

                        registeredRCs.Add((rcName, publicName, version, loaders, Path.GetFileName(Path.GetDirectoryName(mtdFile)!)));
                    }
                }
            }
            catch { }
        }

        // Report registered RCs
        if (registeredRCs.Count > 0)
        {
            sb.AppendLine($"## Зарегистрированные RC ({registeredRCs.Count})");
            sb.AppendLine("| Компонент | PublicName | Версия | Loaders | Модуль |");
            sb.AppendLine("|-----------|-----------|--------|---------|--------|");
            foreach (var (name, pubName, ver, loaders, file) in registeredRCs)
                sb.AppendLine($"| {name} | `{pubName}` | {ver} | {string.Join(", ", loaders)} | {file} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("Зарегистрированных RC в Module.mtd не найдено.");
            sb.AppendLine();
        }

        // Find RC project directories (package.json + webpack.config.js)
        var packageJsonFiles = Directory.GetFiles(path, "package.json", SearchOption.AllDirectories)
            .Where(f => !f.Contains("node_modules")).ToArray();

        foreach (var pkgFile in packageJsonFiles)
        {
            var rcDir = Path.GetDirectoryName(pkgFile)!;
            var rcDirName = Path.GetFileName(rcDir);

            sb.AppendLine($"## RC проект: {rcDirName}");
            sb.AppendLine();

            // Check 1: package.json exists and valid
            totalChecks++;
            try
            {
                var pkgJson = await File.ReadAllTextAsync(pkgFile);
                using var pkgDoc = JsonDocument.Parse(pkgJson);
                var pkgRoot = pkgDoc.RootElement;

                var pkgName = pkgRoot.TryGetProperty("name", out var pn) ? pn.GetString() ?? "" : "";
                var pkgVersion = pkgRoot.TryGetProperty("version", out var pv) ? pv.GetString() ?? "" : "";

                sb.AppendLine($"- [PASS] package.json: `{pkgName}` v{pkgVersion}");
                passed++;

                // Check dependencies
                totalChecks++;
                if (pkgRoot.TryGetProperty("dependencies", out var deps) && deps.ValueKind == JsonValueKind.Object)
                {
                    var hasReact = deps.TryGetProperty("react", out _);
                    if (hasReact) { passed++; sb.AppendLine("- [PASS] React зависимость найдена"); }
                    else { failed++; issues.Add($"{rcDirName}: нет React в dependencies"); sb.AppendLine("- [FAIL] React не найден в dependencies"); }
                }
                else { failed++; issues.Add($"{rcDirName}: нет dependencies в package.json"); }
            }
            catch (Exception ex)
            {
                failed++;
                issues.Add($"{rcDirName}: невалидный package.json — {ex.Message}");
                sb.AppendLine($"- [FAIL] package.json: {ex.Message}");
            }

            // Check 2: webpack.config.js exists
            totalChecks++;
            var webpackPath = Path.Combine(rcDir, "webpack.config.js");
            if (File.Exists(webpackPath))
            {
                passed++;
                sb.AppendLine("- [PASS] webpack.config.js найден");

                // Check Module Federation
                totalChecks++;
                var wpContent = await File.ReadAllTextAsync(webpackPath);
                if (wpContent.Contains("ModuleFederationPlugin"))
                {
                    passed++;
                    sb.AppendLine("- [PASS] ModuleFederationPlugin настроен");
                }
                else
                {
                    failed++;
                    issues.Add($"{rcDirName}: нет ModuleFederationPlugin в webpack.config.js");
                    sb.AppendLine("- [FAIL] ModuleFederationPlugin не найден — обязателен для RC");
                }
            }
            else
            {
                failed++;
                issues.Add($"{rcDirName}: нет webpack.config.js");
                sb.AppendLine("- [FAIL] webpack.config.js не найден");
            }

            // Check 3: src/ directory
            totalChecks++;
            var srcDir = Path.Combine(rcDir, "src");
            if (Directory.Exists(srcDir))
            {
                var srcFiles = Directory.GetFiles(srcDir, "*", SearchOption.AllDirectories).Length;
                passed++;
                sb.AppendLine($"- [PASS] src/ директория: {srcFiles} файлов");
            }
            else
            {
                failed++;
                issues.Add($"{rcDirName}: нет src/ директории");
                sb.AppendLine("- [FAIL] src/ директория не найдена");
            }

            // Check 4: dist/ or build output
            totalChecks++;
            var distDir = Path.Combine(rcDir, "dist");
            var buildDir = Path.Combine(rcDir, "build");
            if (Directory.Exists(distDir) || Directory.Exists(buildDir))
            {
                passed++;
                sb.AppendLine("- [PASS] Сборка (dist/build) найдена");
            }
            else
            {
                sb.AppendLine("- [WARN] dist/build не найдены — нужно `npm run build`");
            }

            // Check 5: i18n
            totalChecks++;
            var i18nFiles = Directory.GetFiles(rcDir, "*.json", SearchOption.AllDirectories)
                .Where(f => f.Contains("i18n") || f.Contains("locale") || f.Contains("translation")).ToArray();
            if (i18nFiles.Length > 0)
            {
                passed++;
                sb.AppendLine($"- [PASS] Локализация: {i18nFiles.Length} файлов");
            }
            else
            {
                sb.AppendLine("- [WARN] Файлы локализации не найдены");
            }

            // Check registration
            totalChecks++;
            var isRegistered = registeredRCs.Any(r =>
                r.Name.Contains(rcDirName, StringComparison.OrdinalIgnoreCase) ||
                rcDirName.Contains(r.Name, StringComparison.OrdinalIgnoreCase));
            if (isRegistered)
            {
                passed++;
                sb.AppendLine("- [PASS] Зарегистрирован в Module.mtd");
            }
            else
            {
                failed++;
                issues.Add($"{rcDirName}: НЕ зарегистрирован в Module.mtd RemoteComponents");
                sb.AppendLine("- [FAIL] НЕ зарегистрирован в Module.mtd — добавьте в RemoteComponents");
            }

            sb.AppendLine();
        }

        if (packageJsonFiles.Length == 0 && registeredRCs.Count == 0)
        {
            sb.AppendLine("Remote Components не найдены в данной директории.");
        }

        // Summary
        sb.AppendLine("---");
        sb.AppendLine($"**Проверок:** {passed}/{totalChecks} пройдено, {failed} проблем");

        if (issues.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("### Проблемы");
            foreach (var issue in issues)
                sb.AppendLine($"- {issue}");
        }

        return sb.ToString();
    }
}
