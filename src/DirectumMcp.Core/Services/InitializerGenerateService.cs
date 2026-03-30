using System.Text;
using System.Text.Json;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Generates ModuleInitializer code with roles, rights, databook records.
/// </summary>
public class InitializerGenerateService : IPipelineStep
{
    public string ToolName => "generate_initializer";

    public async Task<GenerateInitializerResult> GenerateAsync(
        string modulePath,
        string moduleName,
        string records = "",
        string roles = "",
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
            return Fail("Параметр `modulePath` не может быть пустым.");
        if (string.IsNullOrWhiteSpace(moduleName))
            return Fail("Параметр `moduleName` не может быть пустым.");

        var parsedRecords = ParseRecords(records);
        var parsedRoles = ParseRoles(roles);
        var createdFiles = new List<string>();
        var modifiedFiles = new List<string>();

        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        var sharedDir = Path.Combine(modulePath, $"{moduleName}.Shared");
        Directory.CreateDirectory(serverDir);
        Directory.CreateDirectory(sharedDir);

        // 1. Generate/update ModuleInitializer.cs
        var initPath = Path.Combine(serverDir, "ModuleInitializer.cs");
        var initContent = GenerateInitializerCs(moduleName, parsedRecords, parsedRoles);
        await File.WriteAllTextAsync(initPath, initContent, ct);
        if (File.Exists(initPath)) modifiedFiles.Add("ModuleInitializer.cs");
        else createdFiles.Add("ModuleInitializer.cs");

        // 2. Generate/update ModuleConstants.cs with role GUIDs
        if (parsedRoles.Count > 0)
        {
            var constPath = Path.Combine(sharedDir, "ModuleConstants.cs");
            var constContent = GenerateConstantsWithRoles(moduleName, parsedRoles);
            await File.WriteAllTextAsync(constPath, constContent, ct);
            modifiedFiles.Add("ModuleConstants.cs");
        }

        // 3. Update Module.resx / Module.ru.resx with role names
        if (parsedRoles.Count > 0)
        {
            UpdateResxWithRoles(Path.Combine(sharedDir, "Module.resx"), parsedRoles, false);
            UpdateResxWithRoles(Path.Combine(sharedDir, "Module.ru.resx"), parsedRoles, true);
            modifiedFiles.Add("Module.resx");
            modifiedFiles.Add("Module.ru.resx");
        }

        return new GenerateInitializerResult
        {
            Success = true,
            ModuleName = moduleName,
            RolesCount = parsedRoles.Count,
            RecordsCount = parsedRecords.Sum(r => r.Values.Count),
            EntitiesCount = parsedRecords.Count,
            CreatedFiles = createdFiles,
            ModifiedFiles = modifiedFiles
        };
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        return await GenerateAsync(
            modulePath: GetStr(parameters, "modulePath"),
            moduleName: GetStr(parameters, "moduleName"),
            records: GetStr(parameters, "records"),
            roles: GetStr(parameters, "roles"),
            ct: ct);
    }

    #region Code Generation

    private static string GenerateInitializerCs(string moduleName, List<RecordGroup> records, List<RoleDef> roles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine("using Sungero.Domain.Initialization;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine("    public partial class ModuleInitializer");
        sb.AppendLine("    {");

        // Initializing method
        sb.AppendLine("        public override void Initializing(Sungero.Domain.ModuleInitializingEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            Sungero.Commons.PublicInitializationFunctions.Module.ModuleVersionInit(");
        sb.AppendLine("                this.FirstInitializing,");
        sb.AppendLine("                Constants.Module.Init.Name,");
        sb.AppendLine("                Version.Parse(Constants.Module.Init.FirstVersion));");
        sb.AppendLine("        }");
        sb.AppendLine();

        // FirstInitializing
        sb.AppendLine("        public virtual void FirstInitializing()");
        sb.AppendLine("        {");

        if (roles.Count > 0)
        {
            sb.AppendLine("            InitializationLogger.Debug(\"Init: Создание ролей.\");");
            sb.AppendLine("            CreateRoles();");
            sb.AppendLine();
        }

        if (records.Count > 0)
        {
            sb.AppendLine("            InitializationLogger.Debug(\"Init: Заполнение справочников.\");");
            foreach (var rg in records)
                sb.AppendLine($"            Fill{rg.EntityName}();");
            sb.AppendLine();
        }

        sb.AppendLine("            InitializationLogger.Debug(\"Init: Выдача прав.\");");
        sb.AppendLine("            GrantRights();");
        sb.AppendLine("        }");
        sb.AppendLine();

        // CreateRoles
        if (roles.Count > 0)
        {
            sb.AppendLine("        private void CreateRoles()");
            sb.AppendLine("        {");
            foreach (var role in roles)
            {
                sb.AppendLine($"            Sungero.Docflow.PublicInitializationFunctions.Module.CreateRole(");
                sb.AppendLine($"                Resources.RoleName_{role.Name},");
                sb.AppendLine($"                Resources.RoleDescription_{role.Name},");
                sb.AppendLine($"                Constants.Module.{role.Name}RoleGuid);");
            }
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // Fill methods for each entity
        foreach (var rg in records)
        {
            sb.AppendLine($"        private void Fill{rg.EntityName}()");
            sb.AppendLine("        {");
            foreach (var value in rg.Values)
            {
                sb.AppendLine($"            CreateOrUpdate{rg.EntityName}(\"{EscapeCs(value)}\");");
            }
            sb.AppendLine("        }");
            sb.AppendLine();

            sb.AppendLine($"        private static void CreateOrUpdate{rg.EntityName}(string name)");
            sb.AppendLine("        {");
            sb.AppendLine($"            var entity = {rg.EntityName}s.GetAll(x => x.Name == name).FirstOrDefault();");
            sb.AppendLine("            if (entity == null)");
            sb.AppendLine("            {");
            sb.AppendLine($"                entity = {rg.EntityName}s.Create();");
            sb.AppendLine("                entity.Name = name;");
            sb.AppendLine("                entity.Save();");
            sb.AppendLine($"                InitializationLogger.Debug(\"Init: Создан {{0}}: {{1}}\", \"{rg.EntityName}\", name);");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine();
        }

        // GrantRights
        sb.AppendLine("        private void GrantRights()");
        sb.AppendLine("        {");
        sb.AppendLine("            var allUsers = Roles.AllUsers;");
        foreach (var rg in records)
        {
            sb.AppendLine($"            {rg.EntityName}s.AccessRights.Grant(allUsers, DefaultAccessRightsTypes.Read);");
            sb.AppendLine($"            {rg.EntityName}s.AccessRights.Save();");
        }
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateConstantsWithRoles(string moduleName, List<RoleDef> roles)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}");
        sb.AppendLine("{");
        sb.AppendLine("    partial class Constants");
        sb.AppendLine("    {");
        sb.AppendLine("        public static class Module");
        sb.AppendLine("        {");

        foreach (var role in roles)
        {
            sb.AppendLine($"            public static readonly Guid {role.Name}RoleGuid = new(\"{role.Guid}\");");
        }

        sb.AppendLine();
        sb.AppendLine("            public static class Init");
        sb.AppendLine("            {");
        sb.AppendLine($"                public const string Name = \"{moduleName}\";");
        sb.AppendLine("                public const string FirstVersion = \"0.0.1.0\";");
        sb.AppendLine("            }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void UpdateResxWithRoles(string resxPath, List<RoleDef> roles, bool isRussian)
    {
        string xml;
        if (File.Exists(resxPath))
            xml = File.ReadAllText(resxPath);
        else
            xml = JobScaffoldService.BuildEmptyResx();

        foreach (var role in roles)
        {
            var nameKey = $"RoleName_{role.Name}";
            var descKey = $"RoleDescription_{role.Name}";
            var nameVal = isRussian ? role.DisplayNameRu : role.Name;
            var descVal = isRussian ? role.DescriptionRu : $"Role {role.Name}";

            if (!xml.Contains($"name=\"{nameKey}\""))
                xml = JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
                    $"  <data name=\"{nameKey}\" xml:space=\"preserve\">\n    <value>{nameVal}</value>\n  </data>");

            if (!xml.Contains($"name=\"{descKey}\""))
                xml = JobScaffoldService.InsertDataNodeBeforeRootClose(xml,
                    $"  <data name=\"{descKey}\" xml:space=\"preserve\">\n    <value>{descVal}</value>\n  </data>");
        }

        File.WriteAllText(resxPath, xml);
    }

    #endregion

    #region Parsing

    private static List<RecordGroup> ParseRecords(string records)
    {
        // Format: "LossReason:Высокая цена|Выбрали конкурента|Нет бюджета;DealStage:Новая|В работе|Выиграна|Проиграна"
        var result = new List<RecordGroup>();
        if (string.IsNullOrWhiteSpace(records)) return result;

        foreach (var part in records.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var colonIdx = part.IndexOf(':');
            if (colonIdx <= 0) continue;

            var entityName = part[..colonIdx].Trim();
            var values = part[(colonIdx + 1)..]
                .Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            result.Add(new RecordGroup(entityName, values));
        }
        return result;
    }

    private static List<RoleDef> ParseRoles(string roles)
    {
        // Format: "Admin:Администратор:Роль администратора модуля;Manager:Менеджер:Роль менеджера"
        var result = new List<RoleDef>();
        if (string.IsNullOrWhiteSpace(roles)) return result;

        foreach (var part in roles.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segments = part.Split(':', StringSplitOptions.TrimEntries);
            if (segments.Length < 1) continue;

            var name = segments[0];
            var displayRu = segments.Length > 1 ? segments[1] : name;
            var descRu = segments.Length > 2 ? segments[2] : $"Роль {displayRu}";

            result.Add(new RoleDef(name, displayRu, descRu, Guid.NewGuid().ToString("D")));
        }
        return result;
    }

    #endregion

    #region Helpers

    private static string EscapeCs(string value) => value.Replace("\"", "\\\"");

    private static GenerateInitializerResult Fail(string error) =>
        new() { Success = false, Errors = [error] };

    private static string GetStr(Dictionary<string, JsonElement> p, string key, string def = "") =>
        p.TryGetValue(key, out var el) && el.ValueKind == JsonValueKind.String ? el.GetString() ?? def : def;

    private record RecordGroup(string EntityName, List<string> Values);
    private record RoleDef(string Name, string DisplayNameRu, string DescriptionRu, string Guid);

    #endregion
}

public sealed record GenerateInitializerResult : ServiceResult
{
    public string ModuleName { get; init; } = "";
    public int RolesCount { get; init; }
    public int RecordsCount { get; init; }
    public int EntitiesCount { get; init; }
    public List<string> CreatedFiles { get; init; } = [];
    public List<string> ModifiedFiles { get; init; } = [];

    public override string ToMarkdown()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Инициализатор создан");
        sb.AppendLine();
        sb.AppendLine($"**Модуль:** {ModuleName}");
        sb.AppendLine($"**Ролей:** {RolesCount}");
        sb.AppendLine($"**Справочников:** {EntitiesCount}");
        sb.AppendLine($"**Записей:** {RecordsCount}");
        sb.AppendLine();

        if (ModifiedFiles.Count > 0)
        {
            sb.AppendLine($"### Изменённые файлы ({ModifiedFiles.Count})");
            foreach (var f in ModifiedFiles) sb.AppendLine($"- `{f}`");
        }
        if (CreatedFiles.Count > 0)
        {
            sb.AppendLine($"### Созданные файлы ({CreatedFiles.Count})");
            foreach (var f in CreatedFiles) sb.AppendLine($"- `{f}`");
        }
        return sb.ToString();
    }
}
