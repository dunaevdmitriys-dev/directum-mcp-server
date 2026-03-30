using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModelContextProtocol.Server;

namespace DirectumMcp.Analyze.Tools;

[McpServerToolType]
public class IntegrationTools
{
    [McpServerTool(Name = "trace_integration_points")]
    [Description("Найти ВСЕ точки интеграции модуля: OData IntegrationServiceName, WebAPI endpoints [Public(WebApiRequestType)], AsyncHandlers, Jobs, IsolatedFunctions.")]
    public async Task<string> TraceIntegrationPoints(
        [Description("Путь к модулю или решению")] string path)
    {
        if (!Directory.Exists(path))
            return $"**ОШИБКА**: Директория не найдена: `{path}`";

        var sb = new StringBuilder();
        sb.AppendLine("# Точки интеграции");
        sb.AppendLine();

        // 1. OData IntegrationServiceName from .mtd
        var mtdFiles = Directory.GetFiles(path, "*.mtd", SearchOption.AllDirectories);
        var odataEndpoints = new List<(string Entity, string ServiceName, string File)>();
        var asyncHandlers = new List<(string Name, int Delay, string Strategy, string File)>();
        var jobs = new List<(string Name, string Schedule, string File)>();

        foreach (var mtdFile in mtdFiles)
        {
            try
            {
                var json = await File.ReadAllTextAsync(mtdFile);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var fileName = Path.GetFileName(mtdFile);
                var entityName = root.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";

                // IntegrationServiceName
                if (root.TryGetProperty("IntegrationServiceName", out var isn) && isn.ValueKind == JsonValueKind.String)
                {
                    var serviceName = isn.GetString() ?? "";
                    if (!string.IsNullOrEmpty(serviceName))
                        odataEndpoints.Add((entityName, serviceName, fileName));
                }

                // AsyncHandlers (only in Module.mtd)
                if (root.TryGetProperty("AsyncHandlers", out var ah) && ah.ValueKind == JsonValueKind.Array)
                {
                    foreach (var handler in ah.EnumerateArray())
                    {
                        var handlerName = handler.TryGetProperty("Name", out var hn) ? hn.GetString() ?? "" : "";
                        var delay = handler.TryGetProperty("DelayPeriod", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetInt32() : 0;
                        var strategy = handler.TryGetProperty("DelayStrategy", out var ds) ? ds.GetString() ?? "Regular" : "Regular";
                        strategy = strategy.Replace("DelayStrategy", "");
                        asyncHandlers.Add((handlerName, delay, strategy, fileName));
                    }
                }

                // Jobs
                if (root.TryGetProperty("Jobs", out var jbs) && jbs.ValueKind == JsonValueKind.Array)
                {
                    foreach (var job in jbs.EnumerateArray())
                    {
                        var jobName = job.TryGetProperty("Name", out var jn) ? jn.GetString() ?? "" : "";
                        var schedule = job.TryGetProperty("MonthSchedule", out var ms) ? ms.GetString() ?? "" : "";
                        var timePeriod = job.TryGetProperty("TimePeriod", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetInt32() : 0;
                        var scheduleStr = timePeriod > 0 ? $"каждые {timePeriod} мин" : schedule;
                        jobs.Add((jobName, scheduleStr, fileName));
                    }
                }
            }
            catch { }
        }

        // 2. WebAPI endpoints from .cs files
        var csFiles = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj") && !f.Contains("bin") && !f.Contains(".g.cs")).ToArray();

        var webApiEndpoints = new List<(string Method, string Name, string ReturnType, string HttpMethod, string File)>();
        var remoteFunctions = new List<(string Name, string ReturnType, string File)>();
        var publicFunctions = new List<(string Name, string ReturnType, string File)>();
        var isolatedFunctions = new List<(string Name, string ReturnType, string File)>();

        foreach (var csFile in csFiles)
        {
            var content = await File.ReadAllTextAsync(csFile);
            var fileName = Path.GetFileName(csFile);

            // WebApiRequestType
            var webApiMatches = Regex.Matches(content,
                @"\[Public\(WebApiRequestType\s*=\s*RequestType\.(\w+)\)\]\s*\n\s*public\s+(?:virtual\s+)?(\S+)\s+(\w+)\s*\(");
            foreach (Match m in webApiMatches)
                webApiEndpoints.Add(("WebAPI", m.Groups[3].Value, m.Groups[2].Value, m.Groups[1].Value, fileName));

            // [Remote] functions
            var remoteMatches = Regex.Matches(content,
                @"\[Remote[^\]]*\]\s*\n\s*public\s+(?:static\s+)?(?:virtual\s+)?(\S+)\s+(\w+)\s*\(");
            foreach (Match m in remoteMatches)
                remoteFunctions.Add((m.Groups[2].Value, m.Groups[1].Value, fileName));

            // [Public] without WebApi
            var publicMatches = Regex.Matches(content,
                @"\[Public\]\s*\n\s*public\s+(?:static\s+)?(?:virtual\s+)?(\S+)\s+(\w+)\s*\(");
            foreach (Match m in publicMatches)
                publicFunctions.Add((m.Groups[2].Value, m.Groups[1].Value, fileName));

            // IsolatedFunction patterns
            if (csFile.Contains("Isolated", StringComparison.OrdinalIgnoreCase))
            {
                var isoMatches = Regex.Matches(content,
                    @"public\s+(?:static\s+)?(\S+)\s+(\w+)\s*\([^)]*\)");
                foreach (Match m in isoMatches)
                {
                    if (m.Groups[2].Value is not ("Dispose" or "ToString" or "GetHashCode" or "Equals"))
                        isolatedFunctions.Add((m.Groups[2].Value, m.Groups[1].Value, fileName));
                }
            }
        }

        // Report
        if (webApiEndpoints.Count > 0)
        {
            sb.AppendLine($"## WebAPI Endpoints ({webApiEndpoints.Count})");
            sb.AppendLine("| HTTP | Функция | Возвращает | Файл |");
            sb.AppendLine("|------|---------|-----------|------|");
            foreach (var (_, name, ret, http, file) in webApiEndpoints)
                sb.AppendLine($"| {http} | `{name}` | {ret} | {file} |");
            sb.AppendLine();
        }

        if (odataEndpoints.Count > 0)
        {
            sb.AppendLine($"## OData Endpoints ({odataEndpoints.Count})");
            sb.AppendLine("| Сущность | IntegrationServiceName | Файл |");
            sb.AppendLine("|----------|----------------------|------|");
            foreach (var (entity, sn, file) in odataEndpoints)
                sb.AppendLine($"| {entity} | `{sn}` | {file} |");
            sb.AppendLine();
        }

        if (asyncHandlers.Count > 0)
        {
            sb.AppendLine($"## AsyncHandlers ({asyncHandlers.Count})");
            sb.AppendLine("| Имя | Задержка | Стратегия | Файл |");
            sb.AppendLine("|-----|---------|-----------|------|");
            foreach (var (name, delay, strategy, file) in asyncHandlers)
                sb.AppendLine($"| {name} | {delay} мин | {strategy} | {file} |");
            sb.AppendLine();
        }

        if (jobs.Count > 0)
        {
            sb.AppendLine($"## Jobs ({jobs.Count})");
            sb.AppendLine("| Имя | График | Файл |");
            sb.AppendLine("|-----|--------|------|");
            foreach (var (name, schedule, file) in jobs)
                sb.AppendLine($"| {name} | {schedule} | {file} |");
            sb.AppendLine();
        }

        if (remoteFunctions.Count > 0)
        {
            sb.AppendLine($"## [Remote] Functions ({remoteFunctions.Count})");
            sb.AppendLine("| Функция | Возвращает | Файл |");
            sb.AppendLine("|---------|-----------|------|");
            foreach (var (name, ret, file) in remoteFunctions.Take(30))
                sb.AppendLine($"| `{name}` | {ret} | {file} |");
            if (remoteFunctions.Count > 30)
                sb.AppendLine($"| ...и ещё {remoteFunctions.Count - 30} | | |");
            sb.AppendLine();
        }

        if (publicFunctions.Count > 0)
        {
            sb.AppendLine($"## [Public] Functions ({publicFunctions.Count})");
            sb.AppendLine("| Функция | Возвращает | Файл |");
            sb.AppendLine("|---------|-----------|------|");
            foreach (var (name, ret, file) in publicFunctions.Take(30))
                sb.AppendLine($"| `{name}` | {ret} | {file} |");
            if (publicFunctions.Count > 30)
                sb.AppendLine($"| ...и ещё {publicFunctions.Count - 30} | | |");
            sb.AppendLine();
        }

        if (isolatedFunctions.Count > 0)
        {
            sb.AppendLine($"## Isolated Functions ({isolatedFunctions.Count})");
            sb.AppendLine("| Функция | Возвращает | Файл |");
            sb.AppendLine("|---------|-----------|------|");
            foreach (var (name, ret, file) in isolatedFunctions)
                sb.AppendLine($"| `{name}` | {ret} | {file} |");
            sb.AppendLine();
        }

        // Summary
        var total = webApiEndpoints.Count + odataEndpoints.Count + asyncHandlers.Count +
                    jobs.Count + remoteFunctions.Count + publicFunctions.Count + isolatedFunctions.Count;

        sb.AppendLine("---");
        sb.AppendLine($"**Всего точек интеграции:** {total}");
        sb.AppendLine($"- WebAPI: {webApiEndpoints.Count}");
        sb.AppendLine($"- OData: {odataEndpoints.Count}");
        sb.AppendLine($"- AsyncHandlers: {asyncHandlers.Count}");
        sb.AppendLine($"- Jobs: {jobs.Count}");
        sb.AppendLine($"- [Remote]: {remoteFunctions.Count}");
        sb.AppendLine($"- [Public]: {publicFunctions.Count}");
        sb.AppendLine($"- Isolated: {isolatedFunctions.Count}");

        return sb.ToString();
    }
}
