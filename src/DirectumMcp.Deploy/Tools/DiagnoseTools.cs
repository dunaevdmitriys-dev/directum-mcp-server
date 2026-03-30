using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace DirectumMcp.Deploy.Tools;

[McpServerToolType]
public class DiagnoseTools
{
    private static readonly List<ErrorPattern> KnownErrors =
    [
        new("Missing area", @"Missing area|NullReferenceException.*InterfacesGenerator|BaseGenerator",
            "CollectionPropertyMetadata в DatabookEntry",
            "DatabookEntry не может иметь дочерние коллекции (CollectionPropertyMetadata). Либо смените тип на Document, либо удалите коллекцию.",
            "fix_package или вручную: измените BaseGuid на Document (58cca102-1e97-4f07-b6ac-fd866a8b7cb1)"),

        new("Reserved C# word", @"reserved word|is not a valid identifier|CS0116|зарезервированн",
            "Enum value — зарезервированное слово C#",
            "Значения enum (DirectValues.Name) не могут быть зарезервированными словами C# (new, event, class, default и др.)",
            "fix_package dryRun=false — автоматически добавит суффикс 'Value'"),

        new("Duplicate column", @"duplicate column|уже существует столбец|column.*already exists",
            "Дублирующийся Code свойства в иерархии наследования",
            "Два свойства с одинаковым Code в разных сущностях одной иерархии создают дублирующийся столбец в БД.",
            "fix_package dryRun=false — автоматически добавит префикс модуля"),

        new("File lock", @"file.*lock|access.*denied|используется другим процессом|locked|\.csproj.*lock",
            "Файл заблокирован процессом DDS / dotnet",
            "После неудачной сборки DDS не отпускает lock на .csproj файлы (dotnet.exe держит).",
            "Перезапустите DDS. Если не помогает: taskkill /f /im dotnet.exe && taskkill /f /im devenv.exe"),

        new("Resx format", @"ResX|недопустимый ввод|Invalid ResX|resx.*error|Missing resheader",
            "Невалидный формат .resx файла",
            "Отсутствуют обязательные resheader (resmimetype, version, reader, writer) или нарушена XML-структура.",
            "Скопируйте resheader из эталонного .resx (sync_resx_keys) или scaffold_entity для пересоздания"),

        new("AttachmentGroup", @"AttachmentGroup|Constraint.*mismatch|Associated.*group",
            "Несовпадение AttachmentGroup Constraints между Task и Assignment/Notice",
            "AttachmentGroups с IsAssociatedEntityGroup=true должны иметь одинаковые Constraints во всех связанных сущностях.",
            "fix_package dryRun=false — автоматически очистит Constraints до []"),

        new("Analyzers missing", @"Analyzers|analyzer.*not found|\.sds.*Libraries",
            "Отсутствует директория .sds/Libraries/Analyzers/",
            "DDS требует DLL-анализаторы в .sds/Libraries/Analyzers/ для компиляции.",
            "Скопируйте из установки DDS: <DDS_INSTALL>/Analyzers/ → .sds/Libraries/Analyzers/"),

        new("Cover function", @"Can't resolve function|CoverFunction.*not found|FunctionName.*mismatch",
            "FunctionName в CoverFunctionAction не совпадает с методом в ModuleClientFunctions.cs",
            "Действие обложки ссылается на клиентскую функцию, которая не существует или имеет другое имя.",
            "Проверьте: имя метода в ModuleClientFunctions.cs ТОЧНО = FunctionName в Module.mtd Cover.Actions"),

        new("FormTabs", @"FormTabs|FormTab.*not supported",
            "FormTabs не поддерживаются в DDS 25.3",
            "StandaloneFormMetadata не поддерживает FormTabs. Вкладки на карточке невозможны через .mtd.",
            "Удалите секцию FormTabs из .mtd. Используйте ControlGroupMetadata для группировки полей."),

        new("DomainApi version", @"DomainApi|Version.*missing|metadata version",
            "Отсутствует DomainApi версия в Versions",
            "Каждая .mtd ОБЯЗАНА иметь Versions: [{\"Type\":\"DomainApi\",\"Number\":2}]",
            "fix_package dryRun=false — автоматически добавит DomainApi:2"),

        // Issues 2, 8, 10, 12, 14, 15, 16, 17, 18, 19 from DDS_KNOWN_ISSUES.md
        new("Cross-module navigation", @"cross.*module|навигация.*модул|dependency.*missing|EntityGuid.*not found",
            "NavigationProperty ссылается на тип из модуля вне Dependencies",
            "EntityGuid в NavigationPropertyMetadata указывает на сущность чей модуль не объявлен в Dependencies Module.mtd.",
            "Добавьте GUID целевого модуля в Dependencies[] в Module.mtd"),

        new("Resx Property_ format", @"Property_|Resource_[0-9a-f]{8}|подпис.*пуст|label.*empty",
            "System.resx использует Resource_GUID вместо Property_Name",
            "DDS runtime резолвит подписи по ключу Property_<PropertyName>. Формат Resource_<GUID> не работает — подписи будут пустыми.",
            "Заменить все Resource_<GUID> → Property_<PropertyName> в System.resx. Или: sync_resx_keys"),

        new("DisplayName missing", @"DisplayName.*missing|DisplayName.*not found|нет DisplayName",
            "Отсутствует обязательный ресурс DisplayName",
            "Каждая сущность и модуль требуют DisplayName и CollectionDisplayName в System.ru.resx.",
            "Добавьте ключи DisplayName и CollectionDisplayName в System.resx и System.ru.resx"),

        new("Satellite assembly", @"satellite|ресурс.*не найден|resource.*not found|\.resources\.dll",
            "Сателлитная сборка не содержит нужные ресурсы",
            "После изменения .resx без пересборки через DDS — satellite DLL не обновляется.",
            "Пересоберите satellite: resgen → al (Assembly Linker), или повторный импорт через DDS"),

        new("Third-party library", @"третьесторонн|third.?party|NuGet|PackageReference|\.csproj.*modified",
            "Ручное изменение .csproj затирается DDS",
            "DDS управляет .csproj — ручные NuGet-ссылки затираются при каждом импорте.",
            "Добавляйте библиотеки через UI DDS: Модуль → Сторонние библиотеки → +. Имя без точек (NewtonsoftJson, не Newtonsoft.Json)"),

        new("Overridden Controls empty", @"Overridden.*Controls|пустая форма|empty form|Controls.*\[\].*Overridden",
            "Overridden: [Controls] с пустым Controls = пустая форма",
            "Если Controls указан в Overridden, нужно заполнить полный набор контролов. Иначе форма будет пустой.",
            "Либо уберите Controls из Overridden (наследовать базовые), либо заполните все контролы"),

        new("PublicStructures", @"PublicStructure|ModuleStructures\.g\.cs|структур.*не найден",
            "Ошибка в PublicStructures Module.mtd",
            "Свойства структур определяются ТОЛЬКО в Module.mtd → PublicStructures → Properties. ModuleStructures.cs — partial class для доп. кода.",
            "Проверьте Module.mtd → PublicStructures. Имена свойств должны совпадать с ModuleStructures.cs"),

        new("Actions missing fields", @"Action.*DisplayName|Action.*не найден|действи.*ресурс",
            "Действие без обязательных полей",
            "Actions требуют: DisplayName в ResourcesKeys + System.ru.resx, Description/Tooltip, область (Card/Collection).",
            "Добавьте Action_<Name> в System.ru.resx и имя действия в ResourcesKeys Module.mtd"),

        new("JSON serialization mismatch", @"сериализац|deserializ|JsonProperty|property.*name.*mismatch",
            "Несовпадение имён свойств в JSON-сериализации",
            "Имена свойств в сериализаторе ДОЛЖНЫ совпадать с определением в ModuleStructures.cs / Module.mtd.",
            "Проверьте: имена в JSON-запросах/ответах совпадают с PublicStructures"),

        new("SourceTableName", @"SourceTableName|таблица.*не найден|table.*not found|dirrx_",
            "SourceTableName не совпадает с реальной таблицей в БД",
            "DDS генерирует имя таблицы из Code модуля + Code сущности. Если переименовали — таблица не найдётся.",
            "Проверьте Code в .mtd. Для существующих данных используйте SQL ALTER TABLE для переименования")
    ];

    [McpServerTool(Name = "diagnose_build_error")]
    [Description("Диагностика ошибки DDS build: pattern-matching по 19 известным ошибкам, причина + рекомендация + auto-fix.")]
    public Task<string> DiagnoseBuildError(
        [Description("Текст ошибки (скопируйте из DDS / логов)")] string errorText)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Диагностика ошибки DDS");
        sb.AppendLine();
        sb.AppendLine($"**Ошибка:** `{Truncate(errorText, 200)}`");
        sb.AppendLine();

        var matched = new List<ErrorPattern>();
        foreach (var pattern in KnownErrors)
        {
            if (Regex.IsMatch(errorText, pattern.Regex, RegexOptions.IgnoreCase | RegexOptions.Multiline))
                matched.Add(pattern);
        }

        if (matched.Count == 0)
        {
            sb.AppendLine("## Неизвестная ошибка");
            sb.AppendLine();
            sb.AppendLine("Ошибка не соответствует ни одному из 19 известных паттернов.");
            sb.AppendLine();
            sb.AppendLine("### Рекомендации");
            sb.AppendLine("1. `check_package` — может найти структурную причину");
            sb.AppendLine("2. `trace_errors` — полные логи DDS");
            sb.AppendLine("3. `search_metadata` — найдите рабочий аналог в base/ для сравнения");
            sb.AppendLine("4. Перезапустите DDS, если ошибка про file lock");
            sb.AppendLine("5. Проверьте: `directum://knowledge/platform-rules`");
        }
        else
        {
            sb.AppendLine($"## Найдено совпадений: {matched.Count}");
            sb.AppendLine();

            foreach (var (i, m) in matched.Select((m, i) => (i + 1, m)))
            {
                sb.AppendLine($"### {i}. {m.Name}");
                sb.AppendLine();
                sb.AppendLine($"**Причина:** {m.Cause}");
                sb.AppendLine();
                sb.AppendLine($"**Объяснение:** {m.Explanation}");
                sb.AppendLine();
                sb.AppendLine($"**Исправление:** {m.Fix}");
                sb.AppendLine();
            }
        }

        return Task.FromResult(sb.ToString());
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length > maxLen ? text[..maxLen] + "..." : text;

    private record ErrorPattern(string Name, string Regex, string Cause, string Explanation, string Fix);
}
