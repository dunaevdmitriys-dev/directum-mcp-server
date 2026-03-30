using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldXlsxImportTool
{
    [McpServerTool(Name = "scaffold_xlsx_import")]
    [Description("Создать Isolated-обработчик XLSX-импорта: ExcelDataReader, парсинг строк, маппинг полей. Для массового импорта данных.")]
    public async Task<string> ScaffoldXlsxImport(
        [Description("Путь к модулю")] string modulePath,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Имя импортера PascalCase (например 'MetricImporter')")] string importerName,
        [Description("Колонки через запятую: 'Name:string,Amount:double,Date:DateTime'")] string columns = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var isolatedDir = Path.Combine(modulePath, $"{moduleName}.Isolated");
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        Directory.CreateDirectory(isolatedDir);
        Directory.CreateDirectory(serverDir);

        var parsedColumns = ParseColumns(columns);
        var createdFiles = new List<string>();

        // 1. Isolated handler (ExcelDataReader)
        var isolatedCs = GenerateIsolatedHandler(moduleName, importerName, parsedColumns);
        var isolatedPath = Path.Combine(isolatedDir, $"{importerName}.cs");
        await File.WriteAllTextAsync(isolatedPath, isolatedCs);
        createdFiles.Add($"{moduleName}.Isolated/{importerName}.cs");

        // 2. Server function to call isolated
        var serverCs = GenerateServerCaller(moduleName, importerName);
        var serverPath = Path.Combine(serverDir, $"{importerName}Functions.cs");
        await File.WriteAllTextAsync(serverPath, serverCs);
        createdFiles.Add($"{moduleName}.Server/{importerName}Functions.cs");

        // 3. DTO structure suggestion
        var structureNote = GenerateStructureNote(moduleName, importerName, parsedColumns);

        return $"""
            ## XLSX-импортер создан

            **Имя:** {importerName}
            **Модуль:** {moduleName}
            **Колонки:** {(parsedColumns.Count > 0 ? string.Join(", ", parsedColumns.Select(c => $"{c.Name}:{c.Type}")) : "не указаны")}

            ### Созданные файлы
            {string.Join("\n", createdFiles.Select(f => $"- `{f}`"))}

            ### Архитектура
            ```
            Server (вызов) → IsolatedFunctions.{importerName}(bytes)
              → Isolated (парсинг XLSX) → возвращает List<RowDto>
                → Server (сохранение в БД)
            ```

            ### Зависимости (добавить в Isolated.csproj)
            ```xml
            <PackageReference Include="ExcelDataReader" Version="3.6.0" />
            <PackageReference Include="ExcelDataReader.DataSet" Version="3.6.0" />
            <PackageReference Include="System.Text.Encoding.CodePages" Version="8.0.0" />
            ```

            ### PublicStructure для DTO
            {structureNote}

            ### Следующие шаги
            1. Добавьте NuGet пакеты в {moduleName}.Isolated.csproj
            2. Определите PublicStructure в Module.mtd (или используйте DataTable)
            3. Реализуйте маппинг колонок в {importerName}.cs
            4. Вызовите из серверной функции или AsyncHandler
            """;
    }

    private static string GenerateIsolatedHandler(string moduleName, string importerName,
        List<(string Name, string Type)> columns)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using System.Text;");
        sb.AppendLine("using ExcelDataReader;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Isolated");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {importerName}");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Парсинг XLSX файла. Вызывается из IsolatedFunctions.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        public static DataTable Parse(byte[] fileBytes)");
        sb.AppendLine("        {");
        sb.AppendLine("            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);");
        sb.AppendLine();
        sb.AppendLine("            using var stream = new MemoryStream(fileBytes);");
        sb.AppendLine("            using var reader = ExcelReaderFactory.CreateReader(stream);");
        sb.AppendLine("            var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration");
        sb.AppendLine("            {");
        sb.AppendLine("                ConfigureDataTable = (_) => new ExcelDataTableConfiguration");
        sb.AppendLine("                {");
        sb.AppendLine("                    UseHeaderRow = true  // Первая строка — заголовки");
        sb.AppendLine("                }");
        sb.AppendLine("            });");
        sb.AppendLine();
        sb.AppendLine("            if (dataSet.Tables.Count == 0)");
        sb.AppendLine("                throw new InvalidOperationException(\"XLSX файл не содержит листов.\");");
        sb.AppendLine();
        sb.AppendLine("            var table = dataSet.Tables[0];");
        sb.AppendLine();
        sb.AppendLine("            // TODO: Валидация колонок");

        if (columns.Count > 0)
        {
            sb.AppendLine("            // Ожидаемые колонки:");
            foreach (var c in columns)
                sb.AppendLine($"            // - \"{c.Name}\" ({c.Type})");
        }

        sb.AppendLine();
        sb.AppendLine("            return table;");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateServerCaller(string moduleName, string importerName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Data;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine("    partial class ModuleFunctions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Импорт данных из XLSX через {importerName}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        [Remote]");
        sb.AppendLine($"        public static string Import{importerName}(byte[] fileBytes)");
        sb.AppendLine("        {");
        sb.AppendLine($"            var table = IsolatedFunctions.{importerName}.Parse(fileBytes);");
        sb.AppendLine();
        sb.AppendLine("            int imported = 0, errors = 0;");
        sb.AppendLine("            foreach (DataRow row in table.Rows)");
        sb.AppendLine("            {");
        sb.AppendLine("                try");
        sb.AppendLine("                {");
        sb.AppendLine("                    // TODO: Создать/обновить сущность из строки");
        sb.AppendLine("                    // var entity = Entities.Create();");
        sb.AppendLine("                    // entity.Name = row[\"Name\"]?.ToString();");
        sb.AppendLine("                    // entity.Save();");
        sb.AppendLine("                    imported++;");
        sb.AppendLine("                }");
        sb.AppendLine("                catch (Exception ex)");
        sb.AppendLine("                {");
        sb.AppendLine($"                    Logger.Error(\"{moduleName}: Import error row {{0}}: {{1}}\", imported + errors, ex.Message);");
        sb.AppendLine("                    errors++;");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            return $\"Импортировано: {{imported}}, ошибок: {{errors}}\";");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateStructureNote(string moduleName, string importerName,
        List<(string Name, string Type)> columns)
    {
        if (columns.Count == 0) return "_(колонки не указаны — определите вручную)_";

        var sb = new StringBuilder();
        sb.AppendLine("Добавьте в Module.mtd → PublicStructures:");
        sb.AppendLine("```json");
        sb.AppendLine("{");
        sb.AppendLine($"  \"Name\": \"{importerName}Row\",");
        sb.AppendLine("  \"IsPublic\": true,");
        sb.AppendLine($"  \"StructureNamespace\": \"{moduleName}.Structures.Module\",");
        sb.AppendLine("  \"Properties\": [");
        for (int i = 0; i < columns.Count; i++)
        {
            var c = columns[i];
            var comma = i < columns.Count - 1 ? "," : "";
            var globalType = c.Type switch
            {
                "string" => "global::System.String",
                "int" or "long" => "global::System.Int64",
                "double" => "global::System.Double",
                "DateTime" => "global::System.DateTime",
                "bool" => "global::System.Boolean",
                _ => "global::System.String"
            };
            sb.AppendLine($"    {{\"Name\": \"{c.Name}\", \"TypeFullName\": \"{globalType}\", \"IsNullable\": true}}{comma}");
        }
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static List<(string Name, string Type)> ParseColumns(string columns)
    {
        var result = new List<(string, string)>();
        if (string.IsNullOrWhiteSpace(columns)) return result;
        foreach (var part in columns.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var idx = part.IndexOf(':');
            if (idx > 0)
                result.Add((part[..idx].Trim(), part[(idx + 1)..].Trim()));
        }
        return result;
    }
}
