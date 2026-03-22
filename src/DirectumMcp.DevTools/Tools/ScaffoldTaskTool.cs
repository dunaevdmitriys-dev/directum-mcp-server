using System.ComponentModel;
using System.Text;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Services;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldTaskTool
{
    private readonly EntityScaffoldService _entityService = new();

    [McpServerTool(Name = "scaffold_task")]
    [Description("Создать Task + Assignment + Notice — полный комплект для workflow. Координирует GUID между сущностями, AttachmentGroups, формы.")]
    public async Task<string> ScaffoldTask(
        [Description("Путь к директории модуля")] string outputPath,
        [Description("Имя задачи PascalCase (например 'ApprovalRequest')")] string taskName,
        [Description("Полное имя модуля")] string moduleName,
        [Description("Свойства задачи: 'Subject:text,Deadline:date,Priority:enum(High|Normal|Low)'")] string properties = "",
        [Description("Русское название задачи")] string russianName = "",
        [Description("Создать Notice (уведомление)")] bool createNotice = true)
    {
        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        var sb = new StringBuilder();
        var createdFiles = new List<string>();
        var assignmentName = $"{taskName}Assignment";
        var noticeName = $"{taskName}Notice";
        var ruName = string.IsNullOrWhiteSpace(russianName) ? taskName : russianName;

        // 1. Scaffold Task
        var taskResult = await _entityService.ScaffoldAsync(
            outputPath, taskName, moduleName, "Task", "new", properties, russianName: ruName);

        if (!taskResult.Success)
            return $"**ОШИБКА** (Task): {string.Join("; ", taskResult.Errors)}";
        createdFiles.AddRange(taskResult.CreatedFiles);

        // 2. Scaffold Assignment
        var assignmentResult = await _entityService.ScaffoldAsync(
            outputPath, assignmentName, moduleName, "Assignment", "new",
            properties: "", russianName: $"{ruName} (задание)");

        if (!assignmentResult.Success)
            return $"**ОШИБКА** (Assignment): {string.Join("; ", assignmentResult.Errors)}";
        createdFiles.AddRange(assignmentResult.CreatedFiles);

        // 3. Scaffold Notice
        string noticeGuid = "";
        if (createNotice)
        {
            var noticeResult = await _entityService.ScaffoldAsync(
                outputPath, noticeName, moduleName, "Notice", "new",
                properties: "", russianName: $"{ruName} (уведомление)");

            if (!noticeResult.Success)
                return $"**ОШИБКА** (Notice): {string.Join("; ", noticeResult.Errors)}";
            createdFiles.AddRange(noticeResult.CreatedFiles);
            noticeGuid = noticeResult.EntityGuid;
        }

        // 4. Generate workflow block handlers
        var serverDir = Path.Combine(outputPath, "Server");
        Directory.CreateDirectory(serverDir);

        var blockHandlers = GenerateBlockHandlers(moduleName, taskName);
        var blockPath = Path.Combine(serverDir, $"{taskName}BlockHandlers.cs");
        await File.WriteAllTextAsync(blockPath, blockHandlers);
        createdFiles.Add($"Server/{taskName}BlockHandlers.cs");

        // 5. Generate task server handlers
        var taskHandlers = GenerateTaskHandlers(moduleName, taskName);
        var taskHandlerPath = Path.Combine(serverDir, $"{taskName}Handlers.cs");
        await File.WriteAllTextAsync(taskHandlerPath, taskHandlers);
        createdFiles.Add($"Server/{taskName}Handlers.cs");

        // Report
        sb.AppendLine("## Задача создана (Task + Assignment + Notice)");
        sb.AppendLine();
        sb.AppendLine($"**Task:** {taskName} (GUID: {taskResult.EntityGuid})");
        sb.AppendLine($"**Assignment:** {assignmentName} (GUID: {assignmentResult.EntityGuid})");
        if (createNotice)
            sb.AppendLine($"**Notice:** {noticeName} (GUID: {noticeGuid})");
        sb.AppendLine($"**Модуль:** {moduleName}");
        sb.AppendLine();

        sb.AppendLine($"### Созданные файлы ({createdFiles.Count})");
        foreach (var f in createdFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine("1. Добавьте AttachmentGroups в Task.mtd (DocumentGroup, AddendaGroup)");
        sb.AppendLine("2. Синхронизируйте AttachmentGroups в Assignment.mtd и Notice.mtd (Constraints: [])");
        sb.AppendLine("3. Добавьте Blocks в Module.mtd (ScriptBlock, AssignmentBlock)");
        sb.AppendLine("4. Создайте RouteScheme через modify_workflow или вручную");
        sb.AppendLine("5. Реализуйте логику в BlockHandlers.cs");
        sb.AppendLine("6. Запустите check_package для валидации");

        return sb.ToString();
    }

    private static string GenerateBlockHandlers(string moduleName, string taskName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine("using Sungero.Workflow;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {taskName}BlockHandlers");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Блок создания задания исполнителю.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public virtual void CreateAssignmentBlockStart(");
        sb.AppendLine($"            {moduleName}.Server.{taskName}BlockHandlers.CreateAssignmentBlockStartEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            // TODO: Настройте параметры задания");
        sb.AppendLine("            // e.Block.Performers.Add(performer);");
        sb.AppendLine("            // e.Block.Subject = _obj.Subject;");
        sb.AppendLine("            // e.Block.Deadline = _obj.Deadline;");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Обработка результата выполнения задания.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine($"        public virtual void ProcessResultBlockExecute(");
        sb.AppendLine($"            {moduleName}.Server.{taskName}BlockHandlers.ProcessResultBlockExecuteEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            // TODO: Обработайте результат");
        sb.AppendLine("            // var result = e.Block.ExecutionResult;");
        sb.AppendLine("            // if (result == ExecutionResult.Complete) { ... }");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateTaskHandlers(string moduleName, string taskName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using System;");
        sb.AppendLine("using Sungero.Core;");
        sb.AppendLine("using Sungero.CoreEntities;");
        sb.AppendLine();
        sb.AppendLine($"namespace {moduleName}.Server");
        sb.AppendLine("{");
        sb.AppendLine($"    partial class {taskName}ServerHandlers");
        sb.AppendLine("    {");
        sb.AppendLine("        public override void BeforeStart(Sungero.Workflow.Server.BeforeStartEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            base.BeforeStart(e);");
        sb.AppendLine("            // TODO: Валидация перед стартом задачи");
        sb.AppendLine("            // if (_obj.Subject == null) e.AddError(\"Укажите тему\");");
        sb.AppendLine("        }");
        sb.AppendLine();
        sb.AppendLine("        public override void BeforeAbort(Sungero.Workflow.Server.BeforeAbortEventArgs e)");
        sb.AppendLine("        {");
        sb.AppendLine("            base.BeforeAbort(e);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}
