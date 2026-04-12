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
        new("Missing area", @"Missing area|Cannot add area|cannot add collection|NullReferenceException.*InterfacesGenerator|BaseGenerator",
            "CollectionPropertyMetadata в DatabookEntry",
            "DatabookEntry не может иметь дочерние коллекции (CollectionPropertyMetadata). Либо смените тип на Document, либо удалите коллекцию.",
            "fix_package или вручную: измените BaseGuid на Document (58cca102-1e97-4f07-b6ac-fd866a8b7cb1)"),

        new("Reserved C# word", @"reserved word|is not a valid identifier|зарезервированн|'\w+' is a keyword|cannot be used as an identifier|keyword.*conflict",
            "Enum value — зарезервированное слово C#",
            "Значения enum (DirectValues.Name) не могут быть зарезервированными словами C# (new, event, class, default и др.)",
            "fix_package dryRun=false — автоматически добавит суффикс 'Value'"),

        new("Namespace structure error", @"CS0116|namespace cannot directly contain|a namespace cannot contain",
            "Структурная ошибка C# — код вне класса/namespace",
            "Код (метод, свойство) расположен вне partial class, либо нарушена вложенность namespace/class.",
            "Проверьте структуру файла: весь код должен быть внутри partial class, внутри namespace"),

        new("Duplicate column", @"duplicate column|уже существует столбец|column.*already exists",
            "Дублирующийся Code свойства в иерархии наследования",
            "Два свойства с одинаковым Code в разных сущностях одной иерархии создают дублирующийся столбец в БД.",
            "fix_package dryRun=false — автоматически добавит префикс модуля"),

        new("File lock", @"file.*lock|access.*denied|используется другим процессом|locked|\.csproj.*lock|being used by another|cannot access the file|The process cannot access the file|file.*in use|file is being used",
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
        new("Cross-module navigation", @"cross.*module|навигация.*модул|dependency.*missing|EntityGuid.*not found|Missing dependency|missing.*module|module.*not found",
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

        new("SourceTableName", @"SourceTableName|таблица.*не найден|table.*not found|Table.*already exists|duplicate table|dirrx_",
            "SourceTableName конфликт или таблица не найдена в БД",
            "DDS генерирует имя таблицы из Code модуля + Code сущности. Дубликаты таблиц или переименование — частая причина.",
            "Проверьте Code в .mtd на уникальность. Для существующих данных используйте SQL ALTER TABLE для переименования. При дубликатах — измените Code сущности."),

        // Новые паттерны
        new("Assembly load failure", @"Could not load file or assembly|FileNotFoundException.*assembly|assembly.*not found|BadImageFormatException|FileLoadException",
            "Не удаётся загрузить сборку (DLL)",
            "DDS или runtime не находит требуемую DLL. Причины: неправильная версия .NET, отсутствует зависимость, повреждён файл.",
            "Проверьте: 1) Все зависимости в Dependencies Module.mtd. 2) Сторонние DLL через UI DDS. 3) dotnet restore. 4) Пересоберите через DDS."),

        new("Form GUID not found", @"не удалось найти форму|form.*not found|GUID.*not found in entity|FormGuid.*missing|unknown form|форма.*не найден",
            "GUID формы не найден в сущности",
            "StandaloneFormMetadata/RibbonCardMetadata ссылается на FormGuid, который не зарегистрирован в .mtd сущности.",
            "Проверьте FormGuid в .mtd → Forms[]. Если форма наследуется — убедитесь что BaseGuid корректен. extract_entity_schema для проверки GUID."),

        new("DateTime.Now usage", @"DateTime\.(Now|Today|UtcNow)|Calendar\.Now|Calendar\.Today",
            "Использование DateTime.Now вместо Calendar.Now",
            "В Sungero ЗАПРЕЩЕНО DateTime.Now/Today/UtcNow — используйте Calendar.Now/Today для корректной работы с часовыми поясами.",
            "Замените DateTime.Now → Calendar.Now, DateTime.Today → Calendar.Today, DateTime.UtcNow → Calendar.Now.ToUniversalTime()"),

        new("Circular dependency", @"circular.*dependency|циклическ.*зависимост|cycle.*detected|dependency cycle|recursive.*dependency",
            "Циклическая зависимость между модулями",
            "Модуль A зависит от B, а B от A (прямо или транзитивно). DDS не может построить порядок компиляции.",
            "Разорвите цикл: вынесите общие типы в третий модуль, или используйте PublicStructures/WebAPI вместо прямой ссылки. dependency_graph для визуализации."),

        // 4 новых паттерна: area registration, override errors, WebAPI route conflicts, AsyncHandler errors
        new("Area registration", @"area '.*' is not registered|Unknown area|area.*not registered|RegisterArea.*failed|area registration.*failed|unregistered area",
            "Область (area) не зарегистрирована в модуле",
            "Сущность или функция ссылается на area, которая не зарегистрирована в Module.mtd → IsolatedAreas. DDS не может разрешить зависимость.",
            "Добавьте область в Module.mtd → IsolatedAreas[]. Или: validate_isolated_areas для диагностики. Проверьте что area GUID совпадает с определением."),

        new("Override member error", @"Cannot override|Overridden member not found|override.*not found|no suitable method found to override|does not override|cannot override inherited|override mismatch",
            "Ошибка переопределения метода или свойства",
            "Попытка override метода, который не существует в базовом классе, или несовпадение сигнатуры (параметры, возвращаемый тип).",
            "Проверьте: 1) Базовая сущность содержит метод с точно такой же сигнатурой. 2) extract_entity_schema для базового типа. 3) Убедитесь что BaseGuid корректен в .mtd."),

        new("WebAPI route conflict", @"Duplicate route|route.*conflict|duplicate.*endpoint|route pattern.*already|MapRoute.*duplicate|ambiguous route|AmbiguousMatchException.*route|multiple endpoints match",
            "Конфликт маршрутов WebAPI",
            "Два WebAPI endpoint определяют одинаковый route pattern. DDS не может зарегистрировать дублирующийся маршрут.",
            "Проверьте Module.mtd → WebApiEndpoints. Каждый endpoint должен иметь уникальный HttpMethod+Route. Переименуйте конфликтующий route."),

        new("AsyncHandler error", @"AsyncHandler '.*' not found|AsyncHandler.*not registered|handler registration.*failed|async handler.*missing|unknown async handler|AsyncHandler.*не найден|AsyncHandlerGuid.*not found",
            "AsyncHandler не найден или не зарегистрирован",
            "Код ссылается на AsyncHandler, который не определён в Module.mtd → AsyncHandlers, или GUID обработчика не совпадает.",
            "Проверьте: 1) AsyncHandler определён в Module.mtd → AsyncHandlers[]. 2) GUID совпадает в .mtd и коде вызова. 3) scaffold_async_handler для создания нового.")
    ];

    [McpServerTool(Name = "diagnose_build_error")]
    [Description("Диагностика ошибки DDS build: pattern-matching по 29 известным ошибкам, причина + рекомендация + auto-fix.")]
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
            sb.AppendLine("Ошибка не соответствует ни одному из 29 известных паттернов.");
            sb.AppendLine();

            // Умный fallback: анализ ключевых слов ошибки
            sb.AppendLine("### Анализ ключевых слов");
            sb.AppendLine();
            var hints = AnalyzeUnknownError(errorText);
            if (hints.Count > 0)
            {
                foreach (var hint in hints)
                    sb.AppendLine($"- {hint}");
                sb.AppendLine();
            }

            sb.AppendLine("### Рекомендации");
            sb.AppendLine("1. `check_package` — может найти структурную причину");
            sb.AppendLine("2. `trace_errors` — полные логи DDS");
            sb.AppendLine("3. `search_metadata` — найдите рабочий аналог в base/ для сравнения");
            sb.AppendLine("4. `docs/platform/DDS_KNOWN_ISSUES.md` — сверка с полным списком известных проблем");
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

    private static List<string> AnalyzeUnknownError(string errorText)
    {
        var hints = new List<string>();
        var lower = errorText.ToLowerInvariant();

        if (lower.Contains("cs0") || lower.Contains("cs1") || lower.Contains("cs2"))
            hints.Add("Ошибка компиляции C# (CSxxxx). Проверьте синтаксис, using-директивы и namespace.");

        if (lower.Contains("null") || lower.Contains("nullreference"))
            hints.Add("NullReferenceException — вероятно отсутствует обязательное поле в .mtd или не инициализирована зависимость.");

        if (lower.Contains("timeout") || lower.Contains("таймаут"))
            hints.Add("Таймаут — DDS или БД не отвечают. Проверьте доступность PostgreSQL и сервисов RX.");

        if (lower.Contains("connection") || lower.Contains("подключен"))
            hints.Add("Ошибка подключения — проверьте строки подключения к БД и доступность сервисов (PostgreSQL :5432, RabbitMQ :5672).");

        if (lower.Contains("permission") || lower.Contains("права") || lower.Contains("403") || lower.Contains("401"))
            hints.Add("Ошибка прав доступа — проверьте учётные данные и права сервисной учётки.");

        if (lower.Contains("import") || lower.Contains("импорт"))
            hints.Add("Ошибка импорта пакета — проверьте PackageInfo.xml, Dependencies и совместимость версий.");

        if (lower.Contains("migration") || lower.Contains("миграц"))
            hints.Add("Ошибка миграции БД — проверьте SQL-скрипты и совместимость схемы. Бэкап БД перед повторной попыткой.");

        if (lower.Contains(".mtd") || lower.Contains("metadata"))
            hints.Add("Ошибка в метаданных (.mtd) — валидируйте через validate_all и сверьте с extract_entity_schema.");

        if (lower.Contains("odata") || lower.Contains("integration"))
            hints.Add("Ошибка OData/Integration Service — проверьте доступность http://localhost/Integration/odata и учётные данные.");

        if (lower.Contains("docker") || lower.Contains("container"))
            hints.Add("Ошибка Docker — проверьте docker compose ps, логи контейнеров и доступность портов.");

        return hints;
    }

    private static string Truncate(string text, int maxLen) =>
        text.Length > maxLen ? text[..maxLen] + "..." : text;

    private record ErrorPattern(string Name, string Regex, string Cause, string Explanation, string Fix);
}
