using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class CheckComponentTool
{
    private static bool IsPathAllowed(string path)
    {
        var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        if (string.IsNullOrEmpty(solutionPath))
            return false;

        var fullPath = Path.GetFullPath(path);
        var allowedPaths = new[]
        {
            Path.GetFullPath(solutionPath),
            Path.GetFullPath(Path.GetTempPath())
        };
        return allowedPaths.Any(bp =>
            bp.Length >= 4 &&
            fullPath.StartsWith(bp, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly HashSet<string> RequiredDeps = new(StringComparer.OrdinalIgnoreCase)
    {
        "react", "react-dom"
    };

    private static readonly HashSet<string> RequiredDevDeps = new(StringComparer.OrdinalIgnoreCase)
    {
        "@directum/sungero-remote-component-types",
        "@directum/sungero-remote-component-metadata-plugin",
        "webpack", "webpack-cli"
    };

    [McpServerTool(Name = "check_component")]
    [Description("Валидация проекта Remote Component (стороннего контрола) Directum RX: структура, manifest, loaders, зависимости, билд, i18n.")]
    public async Task<string> CheckComponent(
        [Description("Путь к корневой директории Remote Component проекта (содержит package.json и webpack.config.js)")] string componentPath)
    {
        if (string.IsNullOrWhiteSpace(componentPath))
            return "**ОШИБКА**: Параметр `componentPath` не может быть пустым.";

        if (!IsPathAllowed(componentPath))
            return $"**ОШИБКА**: Доступ запрещён. Путь `{componentPath}` находится за пределами разрешённых директорий.";

        if (!Directory.Exists(componentPath))
            return $"**ОШИБКА**: Директория не найдена: `{componentPath}`";

        var issues = new List<ComponentIssue>();
        var info = new ComponentInfo();

        // 1. Check package.json
        await CheckPackageJson(componentPath, info, issues);

        // 2. Check webpack.config.js
        await CheckWebpackConfig(componentPath, info, issues);

        // 3. Check component.manifest.js
        await CheckManifest(componentPath, info, issues);

        // 4. Check component.loaders
        await CheckLoaders(componentPath, info, issues);

        // 5. Check loader ↔ manifest consistency
        CheckManifestLoaderConsistency(info, issues);

        // 6. Check dist/ build output
        CheckBuildOutput(componentPath, info, issues);

        // 7. Check i18n files
        CheckI18n(componentPath, info, issues);

        // 8. Check src/controls/ structure
        CheckControlSources(componentPath, info, issues);

        return FormatReport(componentPath, info, issues);
    }

    private static async Task CheckPackageJson(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var packageJsonPath = Path.Combine(root, "package.json");
        if (!File.Exists(packageJsonPath))
        {
            issues.Add(new("Структура", "critical", "Файл `package.json` не найден"));
            return;
        }

        try
        {
            var json = await File.ReadAllTextAsync(packageJsonPath);
            using var doc = JsonDocument.Parse(json);
            var rootEl = doc.RootElement;

            info.Name = GetString(rootEl, "name");
            info.Version = GetString(rootEl, "version");

            // Check dependencies
            var deps = GetObjectKeys(rootEl, "dependencies");
            var devDeps = GetObjectKeys(rootEl, "devDependencies");

            foreach (var req in RequiredDeps)
            {
                if (!deps.Contains(req))
                    issues.Add(new("Зависимости", "warning", $"Отсутствует зависимость `{req}` в dependencies"));
            }

            foreach (var req in RequiredDevDeps)
            {
                if (!devDeps.Contains(req))
                    issues.Add(new("Зависимости", "warning", $"Отсутствует зависимость `{req}` в devDependencies"));
            }

            info.DependencyCount = deps.Count;
            info.DevDependencyCount = devDeps.Count;

            // Check scripts
            var scripts = GetObjectKeys(rootEl, "scripts");
            if (!scripts.Contains("build"))
                issues.Add(new("Структура", "warning", "Отсутствует скрипт `build` в package.json"));
        }
        catch (Exception ex)
        {
            issues.Add(new("Структура", "critical", $"Ошибка парсинга package.json: {ex.Message}"));
        }
    }

    private static async Task CheckWebpackConfig(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var webpackPath = Path.Combine(root, "webpack.config.js");
        if (!File.Exists(webpackPath))
        {
            issues.Add(new("Структура", "critical", "Файл `webpack.config.js` не найден"));
            return;
        }

        var content = await File.ReadAllTextAsync(webpackPath);

        // Check Module Federation
        if (!content.Contains("ModuleFederationPlugin", StringComparison.Ordinal))
            issues.Add(new("Webpack", "critical", "`ModuleFederationPlugin` не настроен в webpack.config.js"));

        // Check remoteEntry filename
        if (!content.Contains("remoteEntry", StringComparison.Ordinal))
            issues.Add(new("Webpack", "warning", "Не найдена настройка `filename: 'remoteEntry.js'` в ModuleFederationPlugin"));

        // Check exposes
        if (!content.Contains("exposes", StringComparison.Ordinal))
            issues.Add(new("Webpack", "critical", "Секция `exposes` не найдена — контролы не будут экспортированы"));

        // Check shared React singleton
        if (content.Contains("singleton: true", StringComparison.Ordinal))
            info.HasSharedSingletons = true;
        else
            issues.Add(new("Webpack", "warning", "React не объявлен как `singleton: true` в shared — возможны конфликты версий"));

        // Check metadata plugin
        if (!content.Contains("SungeroRemoteComponentMetadataPlugin", StringComparison.Ordinal) &&
            !content.Contains("sungero-remote-component-metadata-plugin", StringComparison.Ordinal))
            issues.Add(new("Webpack", "warning", "Плагин `SungeroRemoteComponentMetadataPlugin` не используется — metadata.json не будет создан"));

        // Extract federation name
        var nameMatch = Regex.Match(content, @"name:\s*['""]([^'""]+)['""]");
        if (nameMatch.Success)
            info.FederationName = nameMatch.Groups[1].Value;
    }

    private static async Task CheckManifest(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var manifestPath = Path.Combine(root, "component.manifest.js");
        if (!File.Exists(manifestPath))
        {
            issues.Add(new("Структура", "critical", "Файл `component.manifest.js` не найден"));
            return;
        }

        var content = await File.ReadAllTextAsync(manifestPath);

        // Extract control names and loader names
        var controlMatches = Regex.Matches(content, @"name:\s*['""]([^'""]+)['""]");
        var loaderMatches = Regex.Matches(content, @"loaderName:\s*['""]([^'""]+)['""]");
        var scopeMatches = Regex.Matches(content, @"scope:\s*['""]([^'""]+)['""]");

        for (int i = 0; i < controlMatches.Count; i++)
        {
            var controlName = controlMatches[i].Groups[1].Value;
            var loaderName = i < loaderMatches.Count ? loaderMatches[i].Groups[1].Value : "";
            var scope = i < scopeMatches.Count ? scopeMatches[i].Groups[1].Value : "unknown";

            // Skip vendorName/componentName level names
            if (controlName == info.Name || controlName.Contains("."))
                continue;

            info.ManifestControls.Add(new ControlEntry(controlName, loaderName, scope));
        }

        // Extract vendor and component name
        var vendorMatch = Regex.Match(content, @"vendorName:\s*['""]([^'""]+)['""]");
        var compMatch = Regex.Match(content, @"componentName:\s*['""]([^'""]+)['""]");
        if (vendorMatch.Success)
            info.VendorName = vendorMatch.Groups[1].Value;
        if (compMatch.Success)
            info.ComponentName = compMatch.Groups[1].Value;

        if (info.ManifestControls.Count == 0)
            issues.Add(new("Manifest", "warning", "Не найдено ни одного контрола в component.manifest.js"));
    }

    private static async Task CheckLoaders(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var loadersFile = Path.Combine(root, "component.loaders.ts");
        if (!File.Exists(loadersFile))
        {
            loadersFile = Path.Combine(root, "component.loaders.js");
            if (!File.Exists(loadersFile))
            {
                issues.Add(new("Структура", "critical", "Файл `component.loaders.ts` (или .js) не найден"));
                return;
            }
        }

        var content = await File.ReadAllTextAsync(loadersFile);

        // Extract exported loader names
        var exportMatches = Regex.Matches(content, @"export\s*\{\s*([^}]+)\}");
        foreach (Match m in exportMatches)
        {
            var names = m.Groups[1].Value.Split(',')
                .Select(n => n.Trim().Split(' ')[0].Trim())
                .Where(n => !string.IsNullOrEmpty(n));
            foreach (var name in names)
                info.ExportedLoaders.Add(name);
        }

        // Also check for direct export declarations
        var directExports = Regex.Matches(content, @"export\s+(?:const|function|async\s+function)\s+(\w+)");
        foreach (Match m in directExports)
            info.ExportedLoaders.Add(m.Groups[1].Value);

        // Check for import patterns (loader files)
        var importMatches = Regex.Matches(content, @"from\s+['""]\.\/src\/loaders\/([^'""]+)['""]");
        foreach (Match m in importMatches)
            info.LoaderImports.Add(m.Groups[1].Value);
    }

    private static void CheckManifestLoaderConsistency(ComponentInfo info, List<ComponentIssue> issues)
    {
        foreach (var control in info.ManifestControls)
        {
            if (string.IsNullOrEmpty(control.LoaderName))
            {
                issues.Add(new("Согласованность", "warning",
                    $"Контрол `{control.Name}` в manifest не имеет loaderName"));
                continue;
            }

            // Check that loader is exported
            var loaderNameNormalized = control.LoaderName.Replace("-", "");
            var found = info.ExportedLoaders.Any(l =>
                l.Replace("-", "").Equals(loaderNameNormalized, StringComparison.OrdinalIgnoreCase) ||
                l.Contains(control.LoaderName, StringComparison.OrdinalIgnoreCase));

            if (!found && info.ExportedLoaders.Count > 0)
            {
                issues.Add(new("Согласованность", "critical",
                    $"Loader `{control.LoaderName}` для контрола `{control.Name}` не найден в экспортах component.loaders"));
            }
        }
    }

    private static void CheckBuildOutput(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var distDir = Path.Combine(root, "dist");
        if (!Directory.Exists(distDir))
        {
            issues.Add(new("Билд", "warning", "Директория `dist/` не найдена — проект не собран"));
            info.IsBuilt = false;
            return;
        }

        var remoteEntry = Path.Combine(distDir, "remoteEntry.js");
        if (!File.Exists(remoteEntry))
        {
            issues.Add(new("Билд", "critical", "Файл `dist/remoteEntry.js` не найден — Module Federation entry point отсутствует"));
            info.IsBuilt = false;
            return;
        }

        info.IsBuilt = true;
        info.RemoteEntrySize = new FileInfo(remoteEntry).Length;
        info.BuildTime = File.GetLastWriteTime(remoteEntry);

        var metadataJson = Path.Combine(distDir, "metadata.json");
        if (!File.Exists(metadataJson))
            issues.Add(new("Билд", "warning", "Файл `dist/metadata.json` не найден — runtime не сможет обнаружить контролы"));
        else
            info.HasMetadataJson = true;

        // Check if source is newer than build
        var sourceFiles = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => !f.Contains("dist") && !f.Contains("node_modules") &&
                        (f.EndsWith(".ts") || f.EndsWith(".tsx") || f.EndsWith(".js") || f.EndsWith(".jsx") || f.EndsWith(".css")));

        var newestSource = sourceFiles
            .Select(f => File.GetLastWriteTime(f))
            .DefaultIfEmpty(DateTime.MinValue)
            .Max();

        if (newestSource > info.BuildTime)
        {
            issues.Add(new("Билд", "warning",
                $"Исходники новее билда (последнее изменение: {newestSource:dd.MM.yyyy HH:mm}, билд: {info.BuildTime:dd.MM.yyyy HH:mm}) — требуется пересборка"));
            info.NeedsRebuild = true;
        }

        // Count dist files
        info.DistFileCount = Directory.GetFiles(distDir).Length;
    }

    private static void CheckI18n(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var localesDir = Path.Combine(root, "locales");
        if (!Directory.Exists(localesDir))
        {
            // Not critical — i18n is optional
            info.HasI18n = false;
            return;
        }

        info.HasI18n = true;

        var ruDir = Path.Combine(localesDir, "ru");
        var enDir = Path.Combine(localesDir, "en");

        var hasRu = Directory.Exists(ruDir) && Directory.GetFiles(ruDir, "*.json").Length > 0;
        var hasEn = Directory.Exists(enDir) && Directory.GetFiles(enDir, "*.json").Length > 0;

        if (!hasRu)
            issues.Add(new("i18n", "warning", "Отсутствует русская локализация (`locales/ru/`)"));
        if (!hasEn)
            issues.Add(new("i18n", "info", "Отсутствует английская локализация (`locales/en/`)"));

        if (hasRu && hasEn)
        {
            // Check key parity
            try
            {
                var ruFiles = Directory.GetFiles(ruDir, "*.json");
                var enFiles = Directory.GetFiles(enDir, "*.json");
                var ruFileNames = ruFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var enFileNames = enFiles.Select(Path.GetFileName).ToHashSet(StringComparer.OrdinalIgnoreCase);

                foreach (var f in ruFileNames.Except(enFileNames))
                    issues.Add(new("i18n", "warning", $"Файл `{f}` есть в ru, но отсутствует в en"));
                foreach (var f in enFileNames.Except(ruFileNames))
                    issues.Add(new("i18n", "warning", $"Файл `{f}` есть в en, но отсутствует в ru"));
            }
            catch
            {
                // Skip i18n key comparison errors
            }
        }
    }

    private static void CheckControlSources(string root, ComponentInfo info, List<ComponentIssue> issues)
    {
        var controlsDir = Path.Combine(root, "src", "controls");
        if (!Directory.Exists(controlsDir))
        {
            if (info.ManifestControls.Count > 0)
                issues.Add(new("Структура", "warning", "Директория `src/controls/` не найдена, но контролы объявлены в manifest"));
            return;
        }

        var controlDirs = Directory.GetDirectories(controlsDir)
            .Select(Path.GetFileName)
            .Where(n => n != null)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        info.ControlSourceDirs.AddRange(controlDirs!);

        // Check each manifest control has a source directory
        foreach (var control in info.ManifestControls)
        {
            var kebabName = ToKebabCase(control.Name);
            if (!controlDirs.Contains(kebabName) && !controlDirs.Contains(control.Name))
            {
                issues.Add(new("Структура", "info",
                    $"Контрол `{control.Name}` не имеет выделенной директории в `src/controls/`"));
            }
        }

        // Check loaders directory
        var loadersDir = Path.Combine(root, "src", "loaders");
        if (!Directory.Exists(loadersDir))
        {
            if (info.ManifestControls.Count > 0)
                issues.Add(new("Структура", "warning", "Директория `src/loaders/` не найдена"));
        }
        else
        {
            var loaderFiles = Directory.GetFiles(loadersDir, "*loader*")
                .Select(Path.GetFileNameWithoutExtension)
                .ToList();
            info.LoaderFileCount = loaderFiles.Count;
        }
    }

    private static string FormatReport(string path, ComponentInfo info, List<ComponentIssue> issues)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Проверка Remote Component");
        sb.AppendLine();
        sb.AppendLine($"**Путь:** `{path}`");

        if (!string.IsNullOrEmpty(info.Name))
            sb.AppendLine($"**Имя:** {info.Name} v{info.Version}");
        if (!string.IsNullOrEmpty(info.VendorName))
            sb.AppendLine($"**Вендор:** {info.VendorName}");
        if (!string.IsNullOrEmpty(info.ComponentName))
            sb.AppendLine($"**Компонент:** {info.ComponentName}");
        if (!string.IsNullOrEmpty(info.FederationName))
            sb.AppendLine($"**Module Federation:** `{info.FederationName}`");
        sb.AppendLine();

        // Controls table
        if (info.ManifestControls.Count > 0)
        {
            sb.AppendLine($"### Контролы ({info.ManifestControls.Count})");
            sb.AppendLine();
            sb.AppendLine("| Контрол | Loader | Scope |");
            sb.AppendLine("|---------|--------|-------|");
            foreach (var c in info.ManifestControls)
                sb.AppendLine($"| {c.Name} | {c.LoaderName} | {c.Scope} |");
            sb.AppendLine();
        }

        // Build status
        sb.AppendLine("### Статус билда");
        sb.AppendLine();
        if (info.IsBuilt)
        {
            sb.AppendLine($"- Собран: {info.BuildTime:dd.MM.yyyy HH:mm}");
            sb.AppendLine($"- remoteEntry.js: {info.RemoteEntrySize / 1024} KB");
            sb.AppendLine($"- metadata.json: {(info.HasMetadataJson ? "✅" : "❌")}");
            sb.AppendLine($"- Файлов в dist/: {info.DistFileCount}");
            if (info.NeedsRebuild)
                sb.AppendLine($"- ⚠️ Требуется пересборка");
        }
        else
        {
            sb.AppendLine("- ❌ Не собран");
        }
        sb.AppendLine();

        // Issues
        var criticals = issues.Where(i => i.Severity == "critical").ToList();
        var warnings = issues.Where(i => i.Severity == "warning").ToList();
        var infos = issues.Where(i => i.Severity == "info").ToList();

        if (issues.Count > 0)
        {
            sb.AppendLine($"### Проблемы ({issues.Count})");
            sb.AppendLine();

            if (criticals.Count > 0)
            {
                sb.AppendLine("**Критические:**");
                foreach (var i in criticals)
                    sb.AppendLine($"- ❌ [{i.Category}] {i.Message}");
                sb.AppendLine();
            }

            if (warnings.Count > 0)
            {
                sb.AppendLine("**Предупреждения:**");
                foreach (var i in warnings)
                    sb.AppendLine($"- ⚠️ [{i.Category}] {i.Message}");
                sb.AppendLine();
            }

            if (infos.Count > 0)
            {
                sb.AppendLine("**Информация:**");
                foreach (var i in infos)
                    sb.AppendLine($"- ℹ️ [{i.Category}] {i.Message}");
                sb.AppendLine();
            }
        }

        // Summary
        sb.AppendLine("### Итого");
        sb.AppendLine($"- Контролов: {info.ManifestControls.Count}");
        sb.AppendLine($"- Зависимостей: {info.DependencyCount} prod + {info.DevDependencyCount} dev");
        sb.AppendLine($"- i18n: {(info.HasI18n ? "✅" : "нет")}");
        sb.AppendLine($"- Shared singletons: {(info.HasSharedSingletons ? "✅" : "нет")}");
        sb.AppendLine($"- Критических: {criticals.Count}, предупреждений: {warnings.Count}, информация: {infos.Count}");
        sb.AppendLine();

        if (criticals.Count == 0 && warnings.Count == 0)
            sb.AppendLine("✅ Все проверки пройдены");

        return sb.ToString();
    }

    private static string ToKebabCase(string name)
    {
        return Regex.Replace(name, @"([a-z])([A-Z])", "$1-$2").ToLowerInvariant();
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private static HashSet<string> GetObjectKeys(JsonElement root, string propertyName)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty(propertyName, out var obj) && obj.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in obj.EnumerateObject())
                keys.Add(prop.Name);
        }
        return keys;
    }

    private record ComponentIssue(string Category, string Severity, string Message);
    private record ControlEntry(string Name, string LoaderName, string Scope);

    private class ComponentInfo
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string VendorName { get; set; } = "";
        public string ComponentName { get; set; } = "";
        public string FederationName { get; set; } = "";
        public int DependencyCount { get; set; }
        public int DevDependencyCount { get; set; }
        public bool HasSharedSingletons { get; set; }
        public bool IsBuilt { get; set; }
        public long RemoteEntrySize { get; set; }
        public DateTime BuildTime { get; set; }
        public bool HasMetadataJson { get; set; }
        public bool NeedsRebuild { get; set; }
        public int DistFileCount { get; set; }
        public bool HasI18n { get; set; }
        public int LoaderFileCount { get; set; }
        public List<ControlEntry> ManifestControls { get; } = new();
        public HashSet<string> ExportedLoaders { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> LoaderImports { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ControlSourceDirs { get; } = new();
    }
}
