using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class ODataTools
{
    // Known OData entity sets verified on a live stand (SESSION.md §3)
    private static readonly Dictionary<string, string> KnownEntitySets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DirRX.CRMSales:Deal"] = "ICRMSalesDeals",
        ["DirRX.CRMSales:Pipeline"] = "ICRMSalesPipelines",
        ["DirRX.CRMSales:Stage"] = "ICRMSalesStages",
        ["DirRX.CRMSales:Activity"] = "ICRMSalesActivities",
        ["DirRX.CRMSales:LossReason"] = "ICRMSalesLossReasons",
        ["DirRX.CRMMarketing:Lead"] = "ICRMMarketingLeads",
        ["DirRX.CRMMarketing:LeadSource"] = "ICRMMarketingLeadSources",
        ["DirRX.CRMCommon:CRMSettings"] = "ICRMCommonCRMSettingss",
        ["DirRX.CRMSales:DealHistory"] = "ICRMSalesDealHistories",
        ["DirRX.CRMMarketing:ScoringRule"] = "ICRMMarketingScoringRules",
        ["DirRX.CRMCommon:CRMMessage"] = "ICRMCommonCRMMessages",
        ["DirRX.CRMSales:CallLog"] = "ICRMSalesCallLogs",
    };

    // Known DB table names
    private static readonly Dictionary<string, string> KnownTables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["DirRX.CRMSales:Deal"] = "dirrx_crmsale_deal",
        ["DirRX.CRMSales:Pipeline"] = "dirrx_crmsale_pipeline",
        ["DirRX.CRMSales:Stage"] = "dirrx_crmsale_stage",
        ["DirRX.CRMSales:Activity"] = "dirrx_crmsale_activity",
        ["DirRX.CRMSales:LossReason"] = "dirrx_crmsale_lossreasn",
        ["DirRX.CRMMarketing:Lead"] = "dirrx_crmmark_lead",
        ["DirRX.CRMMarketing:LeadSource"] = "dirrx_crmmark_leadsrc",
    };

    [McpServerTool(Name = "predict_odata_name")]
    [Description("Предсказание OData EntitySet name и имени таблицы БД по имени сущности и модуля.")]
    public async Task<string> Execute(
        [Description("Имя сущности (например Deal, Lead, CRMSettings)")] string entityName,
        [Description("Имя модуля (например DirRX.CRMSales). Если не указано — ищет в SOLUTION_PATH")] string? moduleName = null,
        [Description("Путь к .mtd файлу сущности (альтернатива entityName+moduleName)")] string? entityMtdPath = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Предсказание OData / DB имён");
        sb.AppendLine();

        string resolvedModuleName = moduleName ?? "";
        string resolvedEntityName = entityName;
        string? moduleCode = null;

        // If MTD path provided, extract info from it
        if (!string.IsNullOrWhiteSpace(entityMtdPath))
        {
            if (!File.Exists(entityMtdPath))
                return $"**ОШИБКА**: Файл не найден: `{entityMtdPath}`";

            try
            {
                using var doc = await MtdParser.ParseRawAsync(entityMtdPath);
                var root = doc.RootElement;
                resolvedEntityName = root.GetStringProp("Name");
                var code = root.GetStringProp("Code");

                // Find Module.mtd by walking up
                var moduleMtdPath = FindModuleMtd(entityMtdPath);
                if (moduleMtdPath != null)
                {
                    using var moduleDic = await MtdParser.ParseRawAsync(moduleMtdPath);
                    resolvedModuleName = moduleDic.RootElement.GetStringProp("Name");
                    moduleCode = moduleDic.RootElement.GetStringProp("Code");
                }
            }
            catch (Exception ex)
            {
                return $"**ОШИБКА**: Не удалось прочитать MTD: {ex.Message}";
            }
        }
        // Try to find in SOLUTION_PATH
        else if (string.IsNullOrWhiteSpace(moduleName))
        {
            var solutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
            if (!string.IsNullOrEmpty(solutionPath))
            {
                var found = await FindEntityInSolution(solutionPath, entityName);
                if (found != null)
                {
                    resolvedModuleName = found.Value.ModuleName;
                    moduleCode = found.Value.ModuleCode;
                    resolvedEntityName = found.Value.EntityName;
                }
            }
        }

        if (string.IsNullOrEmpty(resolvedModuleName))
        {
            sb.AppendLine("**ПРЕДУПРЕЖДЕНИЕ**: Модуль не определён — предсказание может быть неточным.");
            sb.AppendLine();
        }

        // Check known entity sets first
        var key = $"{resolvedModuleName}:{resolvedEntityName}";
        var isKnown = KnownEntitySets.TryGetValue(key, out var knownEntitySet);
        var isKnownTable = KnownTables.TryGetValue(key, out var knownTable);

        // Predict OData entity set name
        var predictedEntitySet = PredictEntitySetName(resolvedModuleName, resolvedEntityName);

        // Predict DB table name
        var predictedTable = PredictTableName(resolvedModuleName, resolvedEntityName, moduleCode);

        sb.AppendLine($"**Сущность**: `{resolvedEntityName}`");
        sb.AppendLine($"**Модуль**: `{resolvedModuleName}`");
        sb.AppendLine();

        sb.AppendLine("## OData EntitySet");
        sb.AppendLine();
        sb.AppendLine($"**Предсказание**: `{predictedEntitySet}`");
        if (isKnown)
        {
            var match = predictedEntitySet == knownEntitySet ? "СОВПАДАЕТ" : "ОТЛИЧАЕТСЯ";
            sb.AppendLine($"**Верифицировано**: `{knownEntitySet}` ({match})");
        }
        sb.AppendLine();
        sb.AppendLine($"**URL**: `http://localhost/Integration/odata/{predictedEntitySet}`");
        sb.AppendLine();

        sb.AppendLine("## Таблица БД");
        sb.AppendLine();
        sb.AppendLine($"**Предсказание**: `{predictedTable}`");
        if (isKnownTable)
        {
            var match = predictedTable == knownTable ? "СОВПАДАЕТ" : "ОТЛИЧАЕТСЯ";
            sb.AppendLine($"**Верифицировано**: `{knownTable}` ({match})");
        }
        sb.AppendLine();

        sb.AppendLine("## Правила именования");
        sb.AppendLine();
        sb.AppendLine("- **OData EntitySet**: `I` + ModulePrefix + Pluralize(EntityName)");
        sb.AppendLine("- **Таблица БД**: lowercase `prefix_moduleCode_entityCode`");
        sb.AppendLine("- **Плюрализация**: `y→ies`, `s/sh/ch/x→+es`, иначе `+s`");
        sb.AppendLine("- **Особенности**: двойная s (CRMSettings → CRMSettingss), History→Histories");

        return sb.ToString();
    }

    /// <summary>
    /// Predicts the OData EntitySet name using platform conventions.
    /// Format: I + ModulePrefix (without dots) + Pluralize(EntityName)
    /// </summary>
    private static string PredictEntitySetName(string moduleName, string entityName)
    {
        // Extract module prefix: DirRX.CRMSales → CRMSales
        var modulePrefix = ExtractModulePrefix(moduleName);
        var plural = Pluralize(entityName);
        return $"I{modulePrefix}{plural}";
    }

    /// <summary>
    /// Predicts the PostgreSQL table name.
    /// Format: prefix_moduleCode_entityCode (all lowercase)
    /// </summary>
    private static string PredictTableName(string moduleName, string entityName, string? moduleCode)
    {
        var prefix = moduleName.StartsWith("DirRX", StringComparison.OrdinalIgnoreCase) ? "dirrx" : "sungero";

        // Module code: DirRX.CRMSales → crmsale (from Module.mtd Code field)
        // If not available, derive from module name
        var code = moduleCode;
        if (string.IsNullOrEmpty(code))
            code = DeriveModuleCode(moduleName);

        var entityCode = entityName.ToLowerInvariant();
        return $"{prefix}_{code.ToLowerInvariant()}_{entityCode}";
    }

    /// <summary>
    /// Extracts the module-specific prefix from the full module name.
    /// DirRX.CRMSales → CRMSales, Sungero.Company → Company
    /// </summary>
    private static string ExtractModulePrefix(string moduleName)
    {
        if (string.IsNullOrEmpty(moduleName))
            return "";

        var lastDot = moduleName.LastIndexOf('.');
        return lastDot >= 0 ? moduleName[(lastDot + 1)..] : moduleName;
    }

    /// <summary>
    /// Derives a likely module Code from module name when Code is unavailable.
    /// DirRX.CRMSales → crmsale, DirRX.CRMMarketing → crmmark
    /// </summary>
    private static string DeriveModuleCode(string moduleName)
    {
        var prefix = ExtractModulePrefix(moduleName);
        // Known patterns from actual DDS modules
        var knownCodes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CRMSales"] = "crmsale",
            ["CRMMarketing"] = "crmmark",
            ["CRMCommon"] = "crmcomm",
        };

        if (knownCodes.TryGetValue(prefix, out var known))
            return known;

        // General heuristic: first 7 chars lowercase
        return prefix.Length > 7 ? prefix[..7].ToLowerInvariant() : prefix.ToLowerInvariant();
    }

    /// <summary>
    /// English pluralization matching Directum RX platform behavior.
    /// </summary>
    private static string Pluralize(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        // Irregular plurals
        if (name.EndsWith("History", StringComparison.Ordinal))
            return name[..^1] + "ies"; // History → Histories

        if (name.EndsWith("y", StringComparison.Ordinal) &&
            !name.EndsWith("ay", StringComparison.Ordinal) &&
            !name.EndsWith("ey", StringComparison.Ordinal) &&
            !name.EndsWith("oy", StringComparison.Ordinal) &&
            !name.EndsWith("uy", StringComparison.Ordinal))
            return name[..^1] + "ies";

        if (name.EndsWith("s", StringComparison.Ordinal) ||
            name.EndsWith("sh", StringComparison.Ordinal) ||
            name.EndsWith("ch", StringComparison.Ordinal) ||
            name.EndsWith("x", StringComparison.Ordinal) ||
            name.EndsWith("z", StringComparison.Ordinal))
            return name + "es";

        // Default: just add s (including CRMSettings → CRMSettingss — platform behavior)
        return name + "s";
    }

    private static string? FindModuleMtd(string entityMtdPath)
    {
        var dir = Path.GetDirectoryName(entityMtdPath);
        while (dir != null)
        {
            var moduleMtds = Directory.GetFiles(dir, "Module.mtd", SearchOption.TopDirectoryOnly);
            if (moduleMtds.Length > 0) return moduleMtds[0];

            // Check Shared subdirectory
            var sharedDir = Path.Combine(dir, Path.GetFileName(dir) + ".Shared");
            if (Directory.Exists(sharedDir))
            {
                moduleMtds = Directory.GetFiles(sharedDir, "Module.mtd", SearchOption.TopDirectoryOnly);
                if (moduleMtds.Length > 0) return moduleMtds[0];
            }

            dir = Path.GetDirectoryName(dir);
            // Stop at solution root (don't go above work/ or base/)
            if (dir != null)
            {
                var dirName = Path.GetFileName(dir);
                if (dirName == "work" || dirName == "base" || dirName == "git_repository")
                    break;
            }
        }
        return null;
    }

    private static async Task<(string ModuleName, string? ModuleCode, string EntityName)?> FindEntityInSolution(
        string solutionPath, string entityName)
    {
        var mtdFiles = Directory.GetFiles(solutionPath, $"{entityName}.mtd", SearchOption.AllDirectories);
        foreach (var mtdFile in mtdFiles)
        {
            if (Path.GetFileName(mtdFile).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
                continue;

            try
            {
                using var doc = await MtdParser.ParseRawAsync(mtdFile);
                var name = doc.RootElement.GetStringProp("Name");
                if (!string.Equals(name, entityName, StringComparison.OrdinalIgnoreCase))
                    continue;

                var moduleMtdPath = FindModuleMtd(mtdFile);
                if (moduleMtdPath != null)
                {
                    using var moduleDic = await MtdParser.ParseRawAsync(moduleMtdPath);
                    return (
                        moduleDic.RootElement.GetStringProp("Name"),
                        moduleDic.RootElement.GetStringProp("Code"),
                        name
                    );
                }
            }
            catch
            {
                // Skip unparseable files
            }
        }
        return null;
    }
}
