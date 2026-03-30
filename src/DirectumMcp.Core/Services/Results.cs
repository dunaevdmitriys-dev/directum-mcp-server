using System.Text;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Result of scaffold_entity operation.
/// </summary>
public sealed record ScaffoldEntityResult : ServiceResult
{
    public string EntityName { get; init; } = "";
    public string EntityGuid { get; init; } = "";
    public string BaseType { get; init; } = "";
    public string ModuleName { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public List<string> CreatedFiles { get; init; } = [];
    public List<PropertyInfo> Properties { get; init; } = [];

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Сущность создана");
        sb.AppendLine();
        sb.AppendLine($"**Имя:** {EntityName}");
        sb.AppendLine($"**Тип:** {BaseType}");
        sb.AppendLine($"**Модуль:** {ModuleName}");
        sb.AppendLine($"**GUID:** {EntityGuid}");
        sb.AppendLine();

        sb.AppendLine($"### Свойства ({Properties.Count})");
        sb.AppendLine();
        sb.AppendLine("| Имя | Тип | GUID |");
        sb.AppendLine("|-----|-----|------|");
        foreach (var prop in Properties)
            sb.AppendLine($"| {prop.Name} | {prop.RawType} | {prop.Guid} |");
        sb.AppendLine();

        sb.AppendLine($"### Созданные файлы ({CreatedFiles.Count})");
        sb.AppendLine();
        foreach (var f in CreatedFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine("1. Добавьте сущность в Module.mtd");
        sb.AppendLine("2. Запустите check_package для валидации");

        return sb.ToString();
    }

    public record PropertyInfo(string Name, string RawType, string Guid);
}

/// <summary>
/// Result of build_dat operation.
/// </summary>
public sealed record BuildDatResult : ServiceResult
{
    public string OutputPath { get; init; } = "";
    public string Version { get; init; } = "";
    public long FileSizeBytes { get; init; }
    public int FileCount { get; init; }
    public int SourceFileCount { get; init; }
    public int SettingsFileCount { get; init; }
    public bool PackageInfoGenerated { get; init; }

    public override string ToMarkdown()
    {
        var size = FileSizeBytes >= 1024 * 1024
            ? $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
            : FileSizeBytes >= 1024
                ? $"{FileSizeBytes / 1024.0:F1} KB"
                : $"{FileSizeBytes} байт";

        return $"""
            ## Пакет собран

            **Путь:** `{OutputPath}`
            **Размер:** {size}
            **Файлов:** {FileCount}

            ### Состав
            {(SourceFileCount > 0 ? $"- source/: {SourceFileCount} файлов" : "")}
            {(SettingsFileCount > 0 ? $"- settings/: {SettingsFileCount} файлов" : "")}
            - PackageInfo.xml{(PackageInfoGenerated ? " (сгенерирован)" : " (существующий)")}

            ### Версия
            {Version}
            """.Trim();
    }
}

/// <summary>
/// Result of check_package operation.
/// </summary>
public sealed record ValidatePackageResult : ServiceResult
{
    public string PackagePath { get; init; } = "";
    public int MtdCount { get; init; }
    public int ResxCount { get; init; }
    public int PassedCount { get; init; }
    public int FailedCount { get; init; }
    public List<CheckInfo> Checks { get; init; } = [];

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Результат валидации пакета");
        sb.AppendLine();
        sb.AppendLine($"**Пакет**: `{PackagePath}`");
        sb.AppendLine($"**MTD файлов**: {MtdCount}");
        sb.AppendLine($"**System.resx файлов**: {ResxCount}");
        sb.AppendLine();
        sb.AppendLine($"**Итого**: {PassedCount} проверок пройдено, {FailedCount} проблем найдено");

        if (FailedCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("*Для автоисправления: `fix_package`*");
        }
        sb.AppendLine();

        foreach (var (i, check) in Checks.Select((c, i) => (i + 1, c)))
        {
            var status = check.Passed ? "PASS" : "FAIL";
            sb.AppendLine($"## {i}. [{status}] {check.Name}");
            if (check.Issues.Count > 0)
            {
                sb.AppendLine();
                foreach (var issue in check.Issues)
                    sb.AppendLine(issue);
                sb.AppendLine();
                sb.AppendLine($"**Рекомендация**: {check.Fix}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public record CheckInfo(string Name, bool Passed, List<string> Issues, string Fix);
}

/// <summary>
/// Result of fix_package operation.
/// </summary>
public sealed record FixPackageResult : ServiceResult
{
    public string PackagePath { get; init; } = "";
    public int AutoFixedCount { get; init; }
    public int ManualRequiredCount { get; init; }
    public bool DryRun { get; init; }
    public List<ChangeInfo> AutoFixed { get; init; } = [];
    public List<ManualIssueInfo> ManualRequired { get; init; } = [];

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Результат исправления пакета {Path.GetFileName(PackagePath)}");
        sb.AppendLine();

        sb.AppendLine($"## Исправлено автоматически ({AutoFixedCount})");
        sb.AppendLine();
        if (AutoFixed.Count > 0)
        {
            sb.AppendLine("| # | Проверка | Файл | Было | Стало |");
            sb.AppendLine("|---|---------|------|------|-------|");
            int idx = 1;
            foreach (var c in AutoFixed)
                sb.AppendLine($"| {idx++} | {c.CheckId} | `{c.FileName}` | {c.Before} | {c.After} |");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("_(нет проблем для автоматического исправления)_");
            sb.AppendLine();
        }

        sb.AppendLine($"## Требует ручного исправления ({ManualRequiredCount})");
        sb.AppendLine();
        if (ManualRequired.Count > 0)
        {
            sb.AppendLine("| # | Проверка | Файл | Описание |");
            sb.AppendLine("|---|---------|------|----------|");
            int idx = 1;
            foreach (var m in ManualRequired)
                sb.AppendLine($"| {idx++} | {m.CheckId} | `{m.FileName}` | {m.Description} |");
            sb.AppendLine();
        }

        sb.AppendLine("## Итого");
        sb.AppendLine($"- Исправлено автоматически: {AutoFixedCount}");
        sb.AppendLine($"- Требует ручного исправления: {ManualRequiredCount}");
        sb.AppendLine($"- Режим: {(DryRun ? "предпросмотр (dryRun=true)" : "изменения применены")}");

        if (DryRun && AutoFixedCount > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Режим предпросмотра. Запустите с `dryRun=false` для применения исправлений.");
        }

        return sb.ToString();
    }

    public record ChangeInfo(string CheckId, string FileName, string Before, string After);
    public record ManualIssueInfo(string CheckId, string FileName, string Description);
}

/// <summary>
/// Result of scaffold_job operation.
/// </summary>
public sealed record ScaffoldJobResult : ServiceResult
{
    public string JobName { get; init; } = "";
    public string JobGuid { get; init; } = "";
    public string ModuleName { get; init; } = "";
    public string CronSchedule { get; init; } = "";
    public List<string> CreatedFiles { get; init; } = [];
    public List<string> UpdatedFiles { get; init; } = [];

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Фоновое задание создано");
        sb.AppendLine();
        sb.AppendLine($"**Имя:** {JobName}");
        sb.AppendLine($"**Модуль:** {ModuleName}");
        sb.AppendLine($"**GUID:** {JobGuid}");
        sb.AppendLine($"**Расписание (cron):** `{CronSchedule}`");
        sb.AppendLine();

        if (CreatedFiles.Count > 0)
        {
            sb.AppendLine($"### Созданные файлы ({CreatedFiles.Count})");
            sb.AppendLine();
            foreach (var f in CreatedFiles)
                sb.AppendLine($"- `{f}`");
            sb.AppendLine();
        }

        sb.AppendLine($"### Обновлённые файлы ({UpdatedFiles.Count})");
        sb.AppendLine();
        foreach (var f in UpdatedFiles)
            sb.AppendLine($"- `{f}`");

        return sb.ToString();
    }
}

/// <summary>
/// Result of scaffold_module operation.
/// </summary>
public sealed record ScaffoldModuleResult : ServiceResult
{
    public string ModulePath { get; init; } = "";
    public string ModuleGuid { get; init; } = "";
    public string FullName { get; init; } = "";
    public string DisplayNameRu { get; init; } = "";
    public List<string> CreatedFiles { get; init; } = [];

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Модуль создан");
        sb.AppendLine();
        sb.AppendLine($"**Имя:** {FullName}");
        sb.AppendLine($"**GUID:** {ModuleGuid}");
        sb.AppendLine($"**Путь:** `{ModulePath}`");
        sb.AppendLine($"**Отображаемое имя:** {DisplayNameRu}");
        sb.AppendLine();

        sb.AppendLine($"### Созданные файлы ({CreatedFiles.Count})");
        sb.AppendLine();
        foreach (var f in CreatedFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine("1. Добавьте сущности через scaffold_entity");
        sb.AppendLine("2. Добавьте сущность в Module.mtd");
        sb.AppendLine("3. Запустите check_package для валидации");
        sb.AppendLine("4. Соберите .dat через build_dat");

        return sb.ToString();
    }
}

/// <summary>
/// Result of scaffold_function operation.
/// </summary>
public sealed record ScaffoldFunctionResult : ServiceResult
{
    public string FunctionName { get; init; } = "";
    public string Side { get; init; } = "";
    public bool IsPublic { get; init; }
    public bool IsRemote { get; init; }
    public List<string> CreatedFiles { get; init; } = [];
    public List<string> ModifiedFiles { get; init; } = [];
    public bool MtdUpdated { get; init; }

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Функция создана");
        sb.AppendLine();
        sb.AppendLine($"**Имя:** {FunctionName}");
        sb.AppendLine($"**Сторона:** {Side}");
        sb.AppendLine($"**Public:** {IsPublic}");
        sb.AppendLine($"**Remote:** {IsRemote}");
        sb.AppendLine($"**MTD обновлён:** {MtdUpdated}");
        sb.AppendLine();

        if (CreatedFiles.Count > 0)
        {
            sb.AppendLine($"### Созданные файлы ({CreatedFiles.Count})");
            foreach (var f in CreatedFiles)
                sb.AppendLine($"- `{f}`");
            sb.AppendLine();
        }

        if (ModifiedFiles.Count > 0)
        {
            sb.AppendLine($"### Изменённые файлы ({ModifiedFiles.Count})");
            foreach (var f in ModifiedFiles)
                sb.AppendLine($"- `{f}`");
        }

        return sb.ToString();
    }
}

/// <summary>
/// Result of pipeline execution.
/// </summary>
public sealed record PipelineResult : ServiceResult
{
    public List<StepResultInfo> Steps { get; init; } = [];
    public int CompletedCount { get; init; }
    public int TotalCount { get; init; }
    public int? FailedAtStep { get; init; }

    public override string ToMarkdown()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Pipeline Result");
        sb.AppendLine();
        sb.AppendLine($"**Шагов:** {CompletedCount}/{TotalCount}");
        if (FailedAtStep.HasValue)
            sb.AppendLine($"**Ошибка на шаге:** {FailedAtStep.Value + 1}");
        sb.AppendLine();

        foreach (var (i, step) in Steps.Select((s, i) => (i + 1, s)))
        {
            var icon = step.Status switch
            {
                "completed" => "OK",
                "skipped" => "SKIP",
                _ => "FAIL"
            };
            sb.AppendLine($"## {i}. [{icon}] {step.Tool}");
            if (!string.IsNullOrEmpty(step.Summary))
                sb.AppendLine(step.Summary);
            sb.AppendLine();
        }

        return sb.ToString();
    }

    public record StepResultInfo
    {
        public string Tool { get; init; } = "";
        public string Status { get; init; } = "";  // completed, failed, skipped
        public string Summary { get; init; } = "";
        public ServiceResult? Result { get; init; }
    }
}
