using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ValidateDeployTool
{
    [McpServerTool(Name = "validate_deploy")]
    [Description("Проверка стенда после публикации: satellite DLL, RC, WebAPI, resx.")]
    public async Task<string> Execute(
        [Description("Путь к DirectumLauncher")] string launcherPath,
        [Description("Путь к git-репозиторию с исходниками")] string repoPath,
        [Description("Список модулей через запятую (если пусто — автоопределение из work/)")] string modules = "")
    {
        if (string.IsNullOrWhiteSpace(launcherPath))
            return "**ОШИБКА**: Параметр `launcherPath` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(repoPath))
            return "**ОШИБКА**: Параметр `repoPath` не может быть пустым.";

        if (!Directory.Exists(launcherPath))
            return $"**ОШИБКА**: Директория DirectumLauncher не найдена: `{launcherPath}`";
        if (!Directory.Exists(repoPath))
            return $"**ОШИБКА**: Директория репозитория не найдена: `{repoPath}`";

        var workDir = Path.Combine(repoPath, "work");
        if (!Directory.Exists(workDir))
            return $"**ОШИБКА**: Директория `work/` не найдена в репозитории: `{workDir}`";

        var moduleNames = ResolveModules(modules, workDir);
        if (moduleNames.Count == 0)
            return "**ОШИБКА**: Не найдено ни одного модуля в `work/`.";

        var sb = new StringBuilder();
        sb.AppendLine("## Результат проверки стенда");
        sb.AppendLine();

        int totalPass = 0;
        int totalFail = 0;

        foreach (var module in moduleNames)
        {
            sb.AppendLine($"### Модуль: {module}");
            sb.AppendLine();

            var moduleDir = Path.Combine(workDir, module);
            if (!Directory.Exists(moduleDir))
            {
                sb.AppendLine($"> Директория модуля не найдена: `{moduleDir}`");
                sb.AppendLine();
                continue;
            }

            // 1. Satellite DLL freshness
            var (p1, f1) = await CheckSatelliteDll(sb, launcherPath, moduleDir, module);
            totalPass += p1; totalFail += f1;

            // 2. RC bundle integrity
            var (p2, f2) = CheckRemoteComponents(sb, launcherPath, moduleDir, module);
            totalPass += p2; totalFail += f2;

            // 3. WebAPI endpoints
            var (p3, f3) = await CheckWebApiEndpoints(sb, moduleDir, module);
            totalPass += p3; totalFail += f3;

            // 4. DB tables
            var (p4, f4) = await CheckDbTables(sb, moduleDir, module);
            totalPass += p4; totalFail += f4;

            // 5. Init data
            var (p5, f5) = await CheckInitData(sb, moduleDir, module);
            totalPass += p5; totalFail += f5;

            // 6. Cover actions
            var (p6, f6) = await CheckCoverActions(sb, moduleDir, module);
            totalPass += p6; totalFail += f6;

            // 7. Resx format
            var (p7, f7) = await CheckResxFormat(sb, moduleDir, module);
            totalPass += p7; totalFail += f7;
        }

        sb.AppendLine($"### Итого: {totalPass} PASS / {totalFail} FAIL");

        return sb.ToString();
    }

    private static List<string> ResolveModules(string modules, string workDir)
    {
        if (!string.IsNullOrWhiteSpace(modules))
        {
            return modules.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        return Directory.GetDirectories(workDir)
            .Select(Path.GetFileName)
            .Where(n => n is not null && !n.StartsWith('.'))
            .Cast<string>()
            .OrderBy(n => n)
            .ToList();
    }

    #region 1. Satellite DLL freshness

    private static Task<(int pass, int fail)> CheckSatelliteDll(
        StringBuilder sb, string launcherPath, string moduleDir, string module)
    {
        sb.AppendLine("#### Satellite DLL");
        sb.AppendLine("| Файл | Статус | Детали |");
        sb.AppendLine("|------|--------|--------|");

        int pass = 0, fail = 0;

        var layerNames = new[] { "Shared", "ClientBase", "Server" };
        var appliedModulesBase = Path.Combine(launcherPath, "etc", "_builds_bin", "SungeroWebServer", "AppliedModules");

        foreach (var layer in layerNames)
        {
            var layerDir = Path.Combine(moduleDir, $"{module}.{layer}");
            if (!Directory.Exists(layerDir))
                continue;

            var resxFiles = Directory.GetFiles(layerDir, "*.resx", SearchOption.AllDirectories);
            if (resxFiles.Length == 0)
                continue;

            var latestResx = resxFiles.Max(f => File.GetLastWriteTimeUtc(f));
            var dllName = $"{module}.{layer}.resources.dll";
            var dllPath = Path.Combine(appliedModulesBase, "ru", dllName);

            if (!File.Exists(dllPath))
            {
                sb.AppendLine($"| {dllName} | FAIL | DLL отсутствует |");
                fail++;
            }
            else
            {
                var dllTime = File.GetLastWriteTimeUtc(dllPath);
                if (latestResx > dllTime)
                {
                    sb.AppendLine($"| {dllName} | FAIL | DLL: {dllTime:dd.MM.yyyy HH:mm}, RESX: {latestResx:dd.MM.yyyy HH:mm} |");
                    fail++;
                }
                else
                {
                    sb.AppendLine($"| {dllName} | PASS | DLL: {dllTime:dd.MM.yyyy HH:mm}, RESX: {latestResx:dd.MM.yyyy HH:mm} |");
                    pass++;
                }
            }
        }

        if (pass == 0 && fail == 0)
            sb.AppendLine("| — | — | Нет .resx файлов |");

        sb.AppendLine();
        return Task.FromResult((pass, fail));
    }

    #endregion

    #region 2. RC bundle integrity

    private static (int pass, int fail) CheckRemoteComponents(
        StringBuilder sb, string launcherPath, string moduleDir, string module)
    {
        sb.AppendLine("#### Remote Components");
        sb.AppendLine("| Файл | Статус | Детали |");
        sb.AppendLine("|------|--------|--------|");

        int pass = 0, fail = 0;

        var appliedModulesBase = Path.Combine(launcherPath, "etc", "_builds_bin", "SungeroWebServer", "AppliedModules");

        // Search for remoteEntry.js in deployed module directories
        var remoteEntries = new List<string>();
        if (Directory.Exists(appliedModulesBase))
        {
            foreach (var dir in Directory.GetDirectories(appliedModulesBase))
            {
                var dirName = Path.GetFileName(dir);
                if (dirName is null || !dirName.Contains(module, StringComparison.OrdinalIgnoreCase))
                    continue;

                var candidates = Directory.GetFiles(dir, "remoteEntry.js", SearchOption.AllDirectories);
                remoteEntries.AddRange(candidates);
            }
        }

        if (remoteEntries.Count == 0)
        {
            sb.AppendLine("| remoteEntry.js | — | Не найден (модуль может не иметь RC) |");
        }
        else
        {
            foreach (var entry in remoteEntries)
            {
                try
                {
                    var content = File.ReadAllText(entry);
                    var hasLoaders = content.Contains("\"./loaders\"") || content.Contains("\"loaders\"");

                    // Try to find webpack.config.js in repo for comparison
                    var webpackConfigs = Directory.GetFiles(moduleDir, "webpack.config.js", SearchOption.AllDirectories);
                    var exposesInfo = "";

                    if (webpackConfigs.Length > 0)
                    {
                        var wcContent = File.ReadAllText(webpackConfigs[0]);
                        var exposesMatch = Regex.Matches(wcContent, @"[""']\./(\w+)[""']");
                        if (exposesMatch.Count > 0)
                        {
                            var exposeNames = exposesMatch.Select(m => m.Groups[1].Value).Distinct().ToList();
                            var missing = exposeNames.Where(e => !content.Contains($"\"./{e}\"") && !content.Contains($"\"{e}\"")).ToList();
                            if (missing.Count > 0)
                                exposesInfo = $", отсутствуют exposes: {string.Join(", ", missing)}";
                        }
                    }

                    if (hasLoaders)
                    {
                        sb.AppendLine($"| {Path.GetFileName(Path.GetDirectoryName(entry))}/remoteEntry.js | PASS | Содержит loaders{exposesInfo} |");
                        pass++;
                    }
                    else
                    {
                        sb.AppendLine($"| {Path.GetFileName(Path.GetDirectoryName(entry))}/remoteEntry.js | FAIL | Не содержит loaders{exposesInfo} |");
                        fail++;
                    }
                }
                catch
                {
                    sb.AppendLine($"| remoteEntry.js | FAIL | Ошибка чтения файла |");
                    fail++;
                }
            }
        }

        sb.AppendLine();
        return (pass, fail);
    }

    #endregion

    #region 3. WebAPI endpoints

    private static async Task<(int pass, int fail)> CheckWebApiEndpoints(
        StringBuilder sb, string moduleDir, string module)
    {
        sb.AppendLine("#### WebAPI Endpoints");
        sb.AppendLine("| Функция | Тип | Статус |");
        sb.AppendLine("|---------|-----|--------|");

        int pass = 0, fail = 0;

        var serverFunctionsFiles = Directory.GetFiles(moduleDir, "ModuleServerFunctions.cs", SearchOption.AllDirectories);
        if (serverFunctionsFiles.Length == 0)
        {
            // Also check for any ServerFunctions files with WebApi attributes
            serverFunctionsFiles = Directory.GetFiles(moduleDir, "*ServerFunctions.cs", SearchOption.AllDirectories);
        }

        var endpoints = new List<(string name, string type)>();

        foreach (var file in serverFunctionsFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var matches = Regex.Matches(content, @"\[Public\(WebApiRequestType\.(\w+)\)\]\s*(?:public\s+)?.*?\s+(\w+)\s*\(");
                foreach (Match match in matches)
                {
                    endpoints.Add((match.Groups[2].Value, match.Groups[1].Value));
                }
            }
            catch
            {
                // Skip unreadable files
            }
        }

        if (endpoints.Count == 0)
        {
            sb.AppendLine("| — | — | Нет WebAPI endpoint'ов |");
        }
        else
        {
            foreach (var (name, type) in endpoints)
            {
                sb.AppendLine($"| {name} | {type} | PASS Найден |");
                pass++;
            }
        }

        sb.AppendLine();
        return (pass, fail);
    }

    #endregion

    #region 4. DB tables check

    private static async Task<(int pass, int fail)> CheckDbTables(
        StringBuilder sb, string moduleDir, string module)
    {
        sb.AppendLine("#### DB Tables (ожидаемые)");
        sb.AppendLine("| Таблица | Сущность |");
        sb.AppendLine("|---------|----------|");

        int pass = 0, fail = 0;

        // Find Module.mtd to get module Code
        var moduleMtdFiles = Directory.GetFiles(moduleDir, "Module.mtd", SearchOption.AllDirectories);
        string moduleCode = module;

        if (moduleMtdFiles.Length > 0)
        {
            try
            {
                using var doc = await MtdParser.ParseRawAsync(moduleMtdFiles[0]);
                if (doc.RootElement.TryGetProperty("Code", out var codeProp) &&
                    codeProp.ValueKind == JsonValueKind.String)
                {
                    moduleCode = codeProp.GetString() ?? module;
                }
            }
            catch { }
        }

        // Find all entity .mtd files
        var entityMtdFiles = Directory.GetFiles(moduleDir, "*.mtd", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var tables = new List<(string table, string entity)>();

        foreach (var mtdFile in entityMtdFiles)
        {
            try
            {
                using var doc = await MtdParser.ParseRawAsync(mtdFile);
                var root = doc.RootElement;

                var metaType = GetString(root, "$type");
                // Skip non-entity metadata (layers, etc.)
                if (!metaType.Contains("EntityMetadata"))
                    continue;

                var entityName = GetString(root, "Name");
                var entityCode = GetString(root, "Code");
                if (string.IsNullOrEmpty(entityCode))
                    entityCode = entityName;

                if (!string.IsNullOrEmpty(entityName))
                {
                    var tableName = $"{moduleCode}_{entityCode}".ToLowerInvariant();
                    tables.Add((tableName, entityName));
                }
            }
            catch { }
        }

        if (tables.Count == 0)
        {
            sb.AppendLine("| — | Нет сущностей |");
        }
        else
        {
            foreach (var (table, entity) in tables.OrderBy(t => t.table))
            {
                sb.AppendLine($"| {table} | {entity} |");
                pass++;
            }
        }

        sb.AppendLine();
        return (pass, fail);
    }

    #endregion

    #region 5. Init data check

    private static async Task<(int pass, int fail)> CheckInitData(
        StringBuilder sb, string moduleDir, string module)
    {
        sb.AppendLine("#### Init Data (начальные данные)");

        int pass = 0, fail = 0;

        var initFiles = Directory.GetFiles(moduleDir, "ModuleInitializer.cs", SearchOption.AllDirectories);
        if (initFiles.Length == 0)
        {
            sb.AppendLine("> ModuleInitializer.cs не найден.");
            sb.AppendLine();
            return (pass, fail);
        }

        sb.AppendLine("| Вызов | Файл |");
        sb.AppendLine("|-------|------|");

        var found = false;
        foreach (var file in initFiles)
        {
            try
            {
                var content = await File.ReadAllTextAsync(file);
                var relPath = Path.GetRelativePath(moduleDir, file);

                // Look for Create / CreateIfNotExists patterns
                var createMatches = Regex.Matches(content, @"\.(Create|CreateIfNotExists|GetOrCreate)\s*\(");
                foreach (Match match in createMatches)
                {
                    // Get the surrounding line for context
                    var lineStart = content.LastIndexOf('\n', match.Index) + 1;
                    var lineEnd = content.IndexOf('\n', match.Index);
                    if (lineEnd < 0) lineEnd = content.Length;
                    var line = content[lineStart..lineEnd].Trim();

                    sb.AppendLine($"| `{EscapePipe(line)}` | {relPath} |");
                    pass++;
                    found = true;
                }
            }
            catch { }
        }

        if (!found)
            sb.AppendLine("| — | Нет вызовов Create/CreateIfNotExists |");

        sb.AppendLine();
        return (pass, fail);
    }

    #endregion

    #region 6. Cover actions check

    private static async Task<(int pass, int fail)> CheckCoverActions(
        StringBuilder sb, string moduleDir, string module)
    {
        sb.AppendLine("#### Cover Actions");
        sb.AppendLine("| Действие | Статус | Детали |");
        sb.AppendLine("|----------|--------|--------|");

        int pass = 0, fail = 0;

        var moduleMtdFiles = Directory.GetFiles(moduleDir, "Module.mtd", SearchOption.AllDirectories);
        if (moduleMtdFiles.Length == 0)
        {
            sb.AppendLine("| — | — | Module.mtd не найден |");
            sb.AppendLine();
            return (pass, fail);
        }

        // Read client functions
        var clientFuncFiles = Directory.GetFiles(moduleDir, "ModuleClientFunctions.cs", SearchOption.AllDirectories);
        var clientContent = new StringBuilder();
        foreach (var f in clientFuncFiles)
        {
            try { clientContent.AppendLine(await File.ReadAllTextAsync(f)); }
            catch { }
        }
        var clientCode = clientContent.ToString();

        var foundActions = false;

        foreach (var mtdFile in moduleMtdFiles)
        {
            try
            {
                using var doc = await MtdParser.ParseRawAsync(mtdFile);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Actions", out var actions) || actions.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var action in actions.EnumerateArray())
                {
                    var actionType = GetString(action, "$type");
                    if (!actionType.Contains("CoverFunctionActionMetadata"))
                        continue;

                    var functionName = GetString(action, "FunctionName");
                    var actionName = GetString(action, "Name");
                    if (string.IsNullOrEmpty(functionName))
                        continue;

                    foundActions = true;

                    if (ContainsMethodName(clientCode, functionName))
                    {
                        sb.AppendLine($"| {actionName} | PASS | Функция `{functionName}` найдена в ModuleClientFunctions.cs |");
                        pass++;
                    }
                    else
                    {
                        sb.AppendLine($"| {actionName} | FAIL | Функция `{functionName}` НЕ найдена в ModuleClientFunctions.cs |");
                        fail++;
                    }
                }
            }
            catch { }
        }

        if (!foundActions)
            sb.AppendLine("| — | — | Нет CoverFunctionAction в Module.mtd |");

        sb.AppendLine();
        return (pass, fail);
    }

    #endregion

    #region 7. Resx format check

    private static async Task<(int pass, int fail)> CheckResxFormat(
        StringBuilder sb, string moduleDir, string module)
    {
        sb.AppendLine("#### Resx Format");
        sb.AppendLine("| Файл | Статус | Детали |");
        sb.AppendLine("|------|--------|--------|");

        int pass = 0, fail = 0;

        var systemResxFiles = Directory.GetFiles(moduleDir, "*System.resx", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(moduleDir, "*System.ru.resx", SearchOption.AllDirectories))
            .Distinct()
            .ToList();

        if (systemResxFiles.Count == 0)
        {
            sb.AppendLine("| — | — | Нет System.resx файлов |");
            sb.AppendLine();
            return (pass, fail);
        }

        foreach (var resxFile in systemResxFiles)
        {
            try
            {
                var issues = await ResxParser.ValidateSystemResxAsync(resxFile);
                var relPath = Path.GetRelativePath(moduleDir, resxFile);

                if (issues.Count == 0)
                {
                    sb.AppendLine($"| {relPath} | PASS | Все ключи в правильном формате |");
                    pass++;
                }
                else
                {
                    var badKeys = string.Join(", ", issues.Take(3).Select(i => $"`{i.Key}`"));
                    var suffix = issues.Count > 3 ? $" и ещё {issues.Count - 3}" : "";
                    sb.AppendLine($"| {relPath} | FAIL | Resource_GUID ключи: {badKeys}{suffix} |");
                    fail++;
                }
            }
            catch
            {
                var relPath = Path.GetRelativePath(moduleDir, resxFile);
                sb.AppendLine($"| {relPath} | FAIL | Ошибка чтения файла |");
                fail++;
            }
        }

        sb.AppendLine();
        return (pass, fail);
    }

    #endregion

    #region Helpers

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    private static bool ContainsMethodName(string content, string methodName)
    {
        if (string.IsNullOrEmpty(content) || string.IsNullOrEmpty(methodName))
            return false;
        return Regex.IsMatch(content, @"\b" + Regex.Escape(methodName) + @"\s*\(");
    }

    private static string EscapePipe(string text)
    {
        return text.Replace("|", "\\|");
    }

    #endregion
}
