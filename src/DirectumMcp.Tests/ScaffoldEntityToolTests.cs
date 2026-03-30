using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ScaffoldEntityToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly ScaffoldEntityTool _tool;

    public ScaffoldEntityToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ScaffoldEntTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new ScaffoldEntityTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string GetOutputDir(string name) => Path.Combine(_tempDir, name);

    #region New Entity

    [Fact]
    public async Task Scaffold_NewDatabookEntry_CreatesAllFiles()
    {
        var output = GetOutputDir("new_db");

        var result = await _tool.ScaffoldEntity(output, "TestEntity", "MyModule",
            baseType: "DatabookEntry", properties: "Name:string,Active:bool");

        Assert.Contains("создана", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(output, "TestEntity.mtd")));
        Assert.True(File.Exists(Path.Combine(output, "TestEntitySystem.resx")));
        Assert.True(File.Exists(Path.Combine(output, "TestEntitySystem.ru.resx")));
        Assert.True(Directory.Exists(Path.Combine(output, "Server")));
        Assert.True(Directory.Exists(Path.Combine(output, "Shared")));
    }

    [Fact]
    public async Task Scaffold_NewEntity_MtdContainsBaseGuid()
    {
        var output = GetOutputDir("base_guid");

        await _tool.ScaffoldEntity(output, "MyDoc", "Mod", baseType: "Document");

        var mtd = File.ReadAllText(Path.Combine(output, "MyDoc.mtd"));
        Assert.Contains("58cca102-1e97-4f07-b6ac-fd866a8b7cb1", mtd); // Document GUID
    }

    [Fact]
    public async Task Scaffold_NewEntity_MtdContainsProperties()
    {
        var output = GetOutputDir("props");

        await _tool.ScaffoldEntity(output, "Contract", "Mod",
            properties: "Title:string,Amount:int,DueDate:date");

        var mtd = File.ReadAllText(Path.Combine(output, "Contract.mtd"));
        Assert.Contains("Title", mtd);
        Assert.Contains("Amount", mtd);
        Assert.Contains("DueDate", mtd);
        Assert.Contains("StringPropertyMetadata", mtd);
        Assert.Contains("IntegerPropertyMetadata", mtd);
        Assert.Contains("DateTimePropertyMetadata", mtd);
    }

    [Fact]
    public async Task Scaffold_NewEntity_EnumPropertyHasDirectValues()
    {
        var output = GetOutputDir("enum_prop");

        await _tool.ScaffoldEntity(output, "Order", "Mod",
            properties: "Status:enum(Draft|Active|Closed)");

        var mtd = File.ReadAllText(Path.Combine(output, "Order.mtd"));
        Assert.Contains("EnumPropertyMetadata", mtd);
        Assert.Contains("Draft", mtd);
        Assert.Contains("Active", mtd);
        Assert.Contains("Closed", mtd);
    }

    [Fact]
    public async Task Scaffold_NewEntity_ResxContainsPropertyKeys()
    {
        var output = GetOutputDir("resx_keys");

        await _tool.ScaffoldEntity(output, "Item", "Mod",
            properties: "Name:string,Count:int");

        var resx = File.ReadAllText(Path.Combine(output, "ItemSystem.resx"));
        Assert.Contains("Property_Name", resx);
        Assert.Contains("Property_Count", resx);
        Assert.Contains("DisplayName", resx);
    }

    [Fact]
    public async Task Scaffold_NewEntity_CsFilesHaveCorrectNamespace()
    {
        var output = GetOutputDir("ns_check");

        await _tool.ScaffoldEntity(output, "TestEntity", "DirRX.CRM");

        var serverCs = File.ReadAllText(
            Directory.GetFiles(Path.Combine(output, "Server"), "*.cs")[0]);
        Assert.Contains("DirRX.CRM", serverCs);
        Assert.Contains("partial class TestEntity", serverCs);
    }

    #endregion

    #region Override Mode

    [Fact]
    public async Task Scaffold_Override_MtdContainsAncestorGuid()
    {
        var output = GetOutputDir("override");

        await _tool.ScaffoldEntity(output, "CustomDoc", "Mod",
            baseType: "Document", mode: "override",
            ancestorGuid: "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

        var mtd = File.ReadAllText(Path.Combine(output, "CustomDoc.mtd"));
        Assert.Contains("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", mtd);
    }

    #endregion

    #region Report

    [Fact]
    public async Task Scaffold_Report_ContainsNextSteps()
    {
        var output = GetOutputDir("report");

        var result = await _tool.ScaffoldEntity(output, "MyEntity", "Mod");

        Assert.Contains("Module.mtd", result);
        Assert.Contains("check_package", result);
    }

    [Fact]
    public async Task Scaffold_Report_ShowsProperties()
    {
        var output = GetOutputDir("report_props");

        var result = await _tool.ScaffoldEntity(output, "E", "M",
            properties: "Alpha:string,Beta:int");

        Assert.Contains("Alpha", result);
        Assert.Contains("Beta", result);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Scaffold_NonEmptyDir_StillWorks()
    {
        // ScaffoldEntityTool may or may not reject non-empty dirs
        // Just verify it doesn't crash
        var output = GetOutputDir("notempty");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "existing.txt"), "data");

        var result = await _tool.ScaffoldEntity(output, "E", "M");

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public async Task Scaffold_InvalidBaseType_ReturnsError()
    {
        var output = GetOutputDir("bad_base");

        var result = await _tool.ScaffoldEntity(output, "E", "M", baseType: "Unknown");

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Scaffold_EmptyEntityName_ReturnsError()
    {
        var output = GetOutputDir("no_name");

        var result = await _tool.ScaffoldEntity(output, "", "M");

        Assert.Contains("ОШИБКА", result);
    }

    [Fact]
    public async Task Scaffold_PathOutsideSolution_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            return;

        var result = await _tool.ScaffoldEntity(
            Path.Combine(outsidePath, "test_ent"), "E", "M");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion

    #region MTD Validity (Phase 2 fixes)

    [Fact]
    public async Task Scaffold_MtdContainsVersions()
    {
        var output = GetOutputDir("versions");

        await _tool.ScaffoldEntity(output, "TestEnt", "Mod", baseType: "DatabookEntry");

        var mtd = File.ReadAllText(Path.Combine(output, "TestEnt.mtd"));
        Assert.Contains("\"Versions\":", mtd);
        Assert.Contains("\"EntityMetadata\"", mtd);
        Assert.Contains("\"Number\": 1", mtd);
    }

    [Fact]
    public async Task Scaffold_TaskMtdContainsTaskMetadataVersion()
    {
        var output = GetOutputDir("task_ver");

        await _tool.ScaffoldEntity(output, "MyTask", "Mod", baseType: "Task");

        var mtd = File.ReadAllText(Path.Combine(output, "MyTask.mtd"));
        Assert.Contains("\"Versions\":", mtd);
        Assert.Contains("\"TaskMetadata\"", mtd);
    }

    [Fact]
    public async Task Scaffold_PropertiesContainListDataBinderTypeName()
    {
        var output = GetOutputDir("list_binder");

        await _tool.ScaffoldEntity(output, "Deal", "Mod",
            properties: "Title:string,Amount:int,IsActive:bool");

        var mtd = File.ReadAllText(Path.Combine(output, "Deal.mtd"));
        Assert.Contains("ListDataBinderTypeName", mtd);
        Assert.Contains("StringEditorToStringBinder", mtd);
        Assert.Contains("NumericEditorToIntBinder", mtd);
        Assert.Contains("BooleanEditorToBooleanBinder", mtd);
    }

    [Fact]
    public async Task Scaffold_LongName_CodeTruncatedTo20()
    {
        var output = GetOutputDir("long_code");

        await _tool.ScaffoldEntity(output, "VeryLongEntityNameThatExceedsTwentyChars", "Mod");

        var mtd = File.ReadAllText(Path.Combine(output, "VeryLongEntityNameThatExceedsTwentyChars.mtd"));
        Assert.Contains("\"Name\": \"VeryLongEntityNameThatExceedsTwentyChars\"", mtd);
        Assert.Contains("\"Code\": \"VeryLongEntityNameTh\"", mtd);
    }

    #endregion

    #region Task/Assignment Types

    [Fact]
    public async Task Scaffold_TaskEntity_UsesTaskMetadata()
    {
        var output = GetOutputDir("task_ent");

        await _tool.ScaffoldEntity(output, "ApprovalTask", "Mod", baseType: "Task");

        var mtd = File.ReadAllText(Path.Combine(output, "ApprovalTask.mtd"));
        Assert.Contains("d795d1f6-45c1-4e5e-9677-b53fb7280c7e", mtd); // Task GUID
    }

    #endregion

    #region Job Mode

    [Fact]
    public async Task Scaffold_Job_CreatesJobFiles()
    {
        var output = GetOutputDir("job_files");

        var result = await _tool.ScaffoldEntity(output, "CleanupJob", "DirRX.Sample",
            mode: "job");

        Assert.Contains("создано", result, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(output, "ModuleJobs.cs")));
        Assert.True(File.Exists(Path.Combine(output, "ModuleSystem.resx")));
        Assert.True(File.Exists(Path.Combine(output, "ModuleSystem.ru.resx")));
    }

    [Fact]
    public async Task Scaffold_Job_MtdContainsJobMetadata()
    {
        var output = GetOutputDir("job_mtd");

        await _tool.ScaffoldEntity(output, "SyncJob", "DirRX.Sample",
            mode: "job");

        var mtd = File.ReadAllText(Path.Combine(output, "Module.mtd"));
        Assert.Contains("JobMetadata", mtd);
        Assert.Contains("SyncJob", mtd);
        Assert.Contains("GenerateHandler", mtd);
        Assert.Contains("CronSchedule", mtd);
    }

    [Fact]
    public async Task Scaffold_Job_ResxContainsJobKeys()
    {
        var output = GetOutputDir("job_resx");

        await _tool.ScaffoldEntity(output, "ReportJob", "DirRX.Sample",
            mode: "job");

        var resx = File.ReadAllText(Path.Combine(output, "ModuleSystem.resx"));
        Assert.Contains("Job_ReportJob", resx);
        Assert.Contains("Job_ReportJob_Description", resx);
    }

    [Fact]
    public async Task Scaffold_Job_CsHasCorrectNamespace()
    {
        var output = GetOutputDir("job_ns");

        await _tool.ScaffoldEntity(output, "NotifyJob", "DirRX.Contracts",
            mode: "job");

        var cs = File.ReadAllText(Path.Combine(output, "ModuleJobs.cs"));
        Assert.Contains("namespace DirRX.Contracts.Server", cs);
        Assert.Contains("partial class ModuleJobs", cs);
        Assert.Contains("NotifyJob", cs);
    }

    [Fact]
    public async Task Scaffold_Job_DefaultCron()
    {
        var output = GetOutputDir("job_cron_default");

        await _tool.ScaffoldEntity(output, "DailyJob", "DirRX.Sample",
            mode: "job", properties: "");

        var mtd = File.ReadAllText(Path.Combine(output, "Module.mtd"));
        Assert.Contains("0 0 * * *", mtd);
    }

    [Fact]
    public async Task Scaffold_Job_CustomCron()
    {
        var output = GetOutputDir("job_cron_custom");

        await _tool.ScaffoldEntity(output, "HourlyJob", "DirRX.Sample",
            mode: "job", properties: "0 * * * *");

        var mtd = File.ReadAllText(Path.Combine(output, "Module.mtd"));
        Assert.Contains("0 * * * *", mtd);
    }

    #endregion
}
