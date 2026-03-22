using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldWordGeneratorTool
{
    [McpServerTool(Name = "scaffold_word_generator")]
    [Description("Создать Isolated-обработчик генерации Word-документов через Aspose.Words: шаблон + merge fields + постобработка.")]
    public async Task<string> ScaffoldWordGenerator(
        [Description("Путь к модулю")] string modulePath,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Имя генератора PascalCase (например 'ContractGenerator')")] string generatorName,
        [Description("Merge-поля через запятую: 'ClientName,ContractNumber,Date,Amount'")] string mergeFields = "")
    {
        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        var isolatedDir = Path.Combine(modulePath, $"{moduleName}.Isolated");
        var serverDir = Path.Combine(modulePath, $"{moduleName}.Server");
        Directory.CreateDirectory(isolatedDir);
        Directory.CreateDirectory(serverDir);

        var fields = string.IsNullOrWhiteSpace(mergeFields)
            ? new List<string>()
            : mergeFields.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

        var createdFiles = new List<string>();

        // 1. Isolated handler
        var isolatedCs = GenerateIsolatedHandler(moduleName, generatorName, fields);
        await File.WriteAllTextAsync(Path.Combine(isolatedDir, $"{generatorName}.cs"), isolatedCs);
        createdFiles.Add($"{moduleName}.Isolated/{generatorName}.cs");

        // 2. Server caller
        var serverCs = GenerateServerCaller(moduleName, generatorName, fields);
        await File.WriteAllTextAsync(Path.Combine(serverDir, $"{generatorName}Functions.cs"), serverCs);
        createdFiles.Add($"{moduleName}.Server/{generatorName}Functions.cs");

        return $"""
            ## Word-генератор создан

            **Имя:** {generatorName}
            **Модуль:** {moduleName}
            **Merge-поля:** {(fields.Count > 0 ? string.Join(", ", fields) : "не указаны")}

            ### Созданные файлы
            {string.Join("\n", createdFiles.Select(f => $"- `{f}`"))}

            ### Архитектура
            ```
            Server → IsolatedFunctions.{generatorName}.Generate(templateBytes, mergeData)
              → Isolated (Aspose.Words): открыть шаблон → заполнить merge fields → сохранить
                → возвращает byte[] готового документа
            ```

            ### Зависимости (добавить в Isolated.csproj)
            ```xml
            <PackageReference Include="Aspose.Words" Version="24.1.0" />
            ```

            ### Шаблон Word
            Создайте .docx с merge-полями:
            {string.Join("\n", fields.Select(f => $"- «{f}» — Insert → Quick Parts → Field → MergeField"))}

            ### Следующие шаги
            1. Добавьте Aspose.Words NuGet в Isolated.csproj
            2. Создайте шаблон .docx с MergeField полями
            3. Загрузите шаблон как DocumentTemplate в Directum RX
            4. Вызовите генератор из серверной функции
            """;
    }

    private static string GenerateIsolatedHandler(string moduleName, string generatorName, List<string> fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.IO;");
        sb.AppendLine("using Aspose.Words;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Isolated");
        sb.AppendLine("{");
        sb.AppendLine($"    public static class {generatorName}");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Генерация Word-документа из шаблона с merge-полями.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"templateBytes\">Байты шаблона .docx</param>");
        sb.AppendLine("        /// <param name=\"mergeData\">Словарь: имя поля → значение</param>");
        sb.AppendLine("        /// <returns>Байты готового документа</returns>");
        sb.AppendLine("        public static byte[] Generate(byte[] templateBytes, Dictionary<string, string> mergeData)");
        sb.AppendLine("        {");
        sb.AppendLine("            using var templateStream = new MemoryStream(templateBytes);");
        sb.AppendLine("            var doc = new Document(templateStream);");
        sb.AppendLine();
        sb.AppendLine("            // Mail Merge — заполнение полей");
        sb.AppendLine("            var fieldNames = new List<string>();");
        sb.AppendLine("            var fieldValues = new List<string>();");
        sb.AppendLine("            foreach (var (key, value) in mergeData)");
        sb.AppendLine("            {");
        sb.AppendLine("                fieldNames.Add(key);");
        sb.AppendLine("                fieldValues.Add(value ?? string.Empty);");
        sb.AppendLine("            }");
        sb.AppendLine("            doc.MailMerge.Execute(fieldNames.ToArray(), fieldValues.ToArray());");
        sb.AppendLine();
        sb.AppendLine("            // Постобработка: удалить незаполненные поля");
        sb.AppendLine("            doc.MailMerge.DeleteFields();");
        sb.AppendLine();
        sb.AppendLine("            // Сохранить результат");
        sb.AppendLine("            using var outputStream = new MemoryStream();");
        sb.AppendLine("            doc.Save(outputStream, SaveFormat.Docx);");
        sb.AppendLine("            return outputStream.ToArray();");
        sb.AppendLine("        }");

        if (fields.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("        /// <summary>");
            sb.AppendLine("        /// Список ожидаемых merge-полей шаблона.");
            sb.AppendLine("        /// </summary>");
            sb.AppendLine("        public static readonly string[] ExpectedFields = new[]");
            sb.AppendLine("        {");
            for (int i = 0; i < fields.Count; i++)
            {
                var comma = i < fields.Count - 1 ? "," : "";
                sb.AppendLine($"            \"{fields[i]}\"{comma}");
            }
            sb.AppendLine("        };");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateServerCaller(string moduleName, string generatorName, List<string> fields)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.Content;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine("    partial class ModuleFunctions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine($"        /// Генерация документа через {generatorName}.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        [Remote]");
        sb.AppendLine($"        public static byte[] Generate{generatorName}(long templateDocId, Dictionary<string, string> mergeData)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Получить шаблон");
        sb.AppendLine("            var templateDoc = ElectronicDocuments.Get(templateDocId);");
        sb.AppendLine("            if (templateDoc == null)");
        sb.AppendLine("                throw new InvalidOperationException($\"Шаблон {templateDocId} не найден.\");");
        sb.AppendLine();
        sb.AppendLine("            var lastVersion = templateDoc.LastVersion;");
        sb.AppendLine("            if (lastVersion == null)");
        sb.AppendLine("                throw new InvalidOperationException(\"У шаблона нет версий.\");");
        sb.AppendLine();
        sb.AppendLine("            byte[] templateBytes;");
        sb.AppendLine("            using (var stream = new System.IO.MemoryStream())");
        sb.AppendLine("            {");
        sb.AppendLine("                lastVersion.Body.Read().CopyTo(stream);");
        sb.AppendLine("                templateBytes = stream.ToArray();");
        sb.AppendLine("            }");
        sb.AppendLine();
        sb.AppendLine($"            return IsolatedFunctions.{generatorName}.Generate(templateBytes, mergeData);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
