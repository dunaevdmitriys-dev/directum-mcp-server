using System.ComponentModel;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class PermissionTools
{
    private static readonly HashSet<string> KnownAccessRightTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Read", "Update", "Create", "Delete", "Approve", "SendByExchange", "Register", "ChangeAccess"
    };

    [McpServerTool(Name = "check_permissions")]
    [Description("Проверить AccessRights в MTD: пустые права, дубликаты, неизвестные роли.")]
    public async Task<string> CheckPermissions(
        [Description("Путь к директории пакета Directum RX или к конкретному .mtd файлу")] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "**ОШИБКА**: Параметр `path` не может быть пустым.";
        string[] mtdFiles;

        if (File.Exists(path) && path.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
        {
            mtdFiles = [path];
        }
        else if (Directory.Exists(path))
        {
            mtdFiles = Directory.GetFiles(path, "*.mtd", SearchOption.AllDirectories);
        }
        else
        {
            return $"**ОШИБКА**: Путь не найден: `{path}`\nУкажите путь к директории пакета или к .mtd файлу.";
        }

        if (mtdFiles.Length == 0)
            return $"**ОШИБКА**: В директории `{path}` не найдено ни одного .mtd файла.";

        // Load module roles from Module.mtd files located anywhere in the same tree
        var searchRoot = File.Exists(path) ? Path.GetDirectoryName(path)! : path;
        var moduleRoles = await LoadModuleRoles(searchRoot);

        var entityResults = new List<EntityPermissionsResult>();

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Skip Module.mtd files — they hold Roles definitions, not entity AccessRights
                var metaType = root.TryGetProperty("$type", out var t) ? t.GetString() ?? "" : "";
                if (metaType.Contains("ModuleMetadata"))
                    continue;

                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? Path.GetFileNameWithoutExtension(mtdFile) : Path.GetFileNameWithoutExtension(mtdFile);

                var issues = new List<PermissionsIssue>();

                if (!root.TryGetProperty("AccessRights", out var accessRights) ||
                    accessRights.ValueKind != JsonValueKind.Array ||
                    !accessRights.EnumerateArray().Any())
                {
                    issues.Add(new PermissionsIssue(IssueLevel.Warning, "EmptyAccessRights",
                        $"Сущность `{entityName}` не имеет блока AccessRights или он пуст"));
                }
                else
                {
                    CheckDuplicates(accessRights, entityName, issues);
                    CheckUnknownRightTypes(accessRights, entityName, issues);
                    if (moduleRoles.Count > 0)
                        CheckRoleReferences(accessRights, entityName, moduleRoles, issues);
                }

                if (issues.Count > 0)
                    entityResults.Add(new EntityPermissionsResult(entityName, mtdFile, issues));
            }
            catch (JsonException ex)
            {
                entityResults.Add(new EntityPermissionsResult(
                    Path.GetFileNameWithoutExtension(mtdFile),
                    mtdFile,
                    [new PermissionsIssue(IssueLevel.Error, "ParseError", $"Ошибка разбора MTD: {ex.Message}")]));
            }
        }

        return BuildReport(path, mtdFiles.Length, entityResults, moduleRoles.Count > 0);
    }

    private static void CheckDuplicates(JsonElement accessRights, string entityName, List<PermissionsIssue> issues)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in accessRights.EnumerateArray())
        {
            var roleGuid = entry.TryGetProperty("RoleGuid", out var rg) ? rg.GetString() ?? "" : "";
            var rightType = entry.TryGetProperty("AccessRightType", out var art) ? art.GetString() ?? "" : "";
            var isGranted = entry.TryGetProperty("IsGranted", out var ig) && ig.GetBoolean();

            var key = $"{roleGuid}|{rightType}|{isGranted}";
            if (!string.IsNullOrEmpty(roleGuid) && !seen.Add(key))
            {
                issues.Add(new PermissionsIssue(IssueLevel.Error, "DuplicateRight",
                    $"Роль `{roleGuid}` с правом `{rightType}` (IsGranted={isGranted}) указана дважды в `{entityName}`"));
            }
        }
    }

    private static void CheckUnknownRightTypes(JsonElement accessRights, string entityName, List<PermissionsIssue> issues)
    {
        foreach (var entry in accessRights.EnumerateArray())
        {
            if (!entry.TryGetProperty("AccessRightType", out var artEl))
                continue;

            var rightType = artEl.GetString() ?? "";
            if (!string.IsNullOrEmpty(rightType) && !KnownAccessRightTypes.Contains(rightType))
            {
                issues.Add(new PermissionsIssue(IssueLevel.Warning, "UnknownRightType",
                    $"Неизвестный тип права `{rightType}` в сущности `{entityName}`. " +
                    $"Допустимые значения: {string.Join(", ", KnownAccessRightTypes)}"));
            }
        }
    }

    private static void CheckRoleReferences(
        JsonElement accessRights,
        string entityName,
        HashSet<string> moduleRoles,
        List<PermissionsIssue> issues)
    {
        foreach (var entry in accessRights.EnumerateArray())
        {
            if (!entry.TryGetProperty("RoleGuid", out var rgEl))
                continue;

            var roleGuid = rgEl.GetString() ?? "";
            if (!string.IsNullOrEmpty(roleGuid) && !moduleRoles.Contains(roleGuid))
            {
                issues.Add(new PermissionsIssue(IssueLevel.Warning, "UnknownRoleGuid",
                    $"Роль `{roleGuid}` в сущности `{entityName}` не найдена в блоке Roles модуля (Module.mtd)"));
            }
        }
    }

    private static async Task<HashSet<string>> LoadModuleRoles(string searchRoot)
    {
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var moduleMtdFiles = Directory.GetFiles(searchRoot, "Module.mtd", SearchOption.AllDirectories);
        foreach (var moduleFile in moduleMtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(moduleFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("Roles", out var rolesEl) || rolesEl.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var role in rolesEl.EnumerateArray())
                {
                    if (role.TryGetProperty("NameGuid", out var ng))
                    {
                        var guid = ng.GetString();
                        if (!string.IsNullOrEmpty(guid))
                            roles.Add(guid);
                    }
                }
            }
            catch
            {
                // Skip unreadable module files
            }
        }

        return roles;
    }

    private static string BuildReport(
        string path,
        int totalMtdFiles,
        List<EntityPermissionsResult> results,
        bool moduleRolesLoaded)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Проверка прав доступа (AccessRights)");
        sb.AppendLine();
        sb.AppendLine($"**Путь**: `{path}`");
        sb.AppendLine($"**MTD файлов проверено**: {totalMtdFiles}");
        sb.AppendLine($"**Module.mtd с ролями**: {(moduleRolesLoaded ? "найден" : "не найден (проверка RoleGuid пропущена)")}");
        sb.AppendLine();

        if (results.Count == 0)
        {
            sb.AppendLine("**Статус**: Проблем не обнаружено.");
            return sb.ToString();
        }

        var errorCount = results.SelectMany(r => r.Issues).Count(i => i.Level == IssueLevel.Error);
        var warningCount = results.SelectMany(r => r.Issues).Count(i => i.Level == IssueLevel.Warning);

        sb.AppendLine($"**Ошибок**: {errorCount}  **Предупреждений**: {warningCount}");
        sb.AppendLine();

        foreach (var entity in results)
        {
            sb.AppendLine($"## {entity.EntityName}");
            sb.AppendLine($"*Файл*: `{entity.FilePath}`");
            sb.AppendLine();

            foreach (var issue in entity.Issues)
            {
                var prefix = issue.Level == IssueLevel.Error ? "**[Error]**" : "[Warning]";
                sb.AppendLine($"- {prefix} `{issue.CheckName}`: {issue.Message}");
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private enum IssueLevel { Warning, Error }

    private record PermissionsIssue(IssueLevel Level, string CheckName, string Message);

    private record EntityPermissionsResult(string EntityName, string FilePath, List<PermissionsIssue> Issues);
}
