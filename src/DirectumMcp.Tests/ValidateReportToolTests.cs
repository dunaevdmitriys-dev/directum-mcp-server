using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ValidateReportToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly ValidateReportTool _tool;

    public ValidateReportToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ValidateReportTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new ValidateReportTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    #region Helpers

    private string CreateReportDir(string reportName)
    {
        var dir = Path.Combine(_tempDir, reportName);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void CreateFrx(string dir, string reportName, IEnumerable<string> dataSources, bool includeConnectionString = false)
    {
        var dataBands = string.Join("\n    ", dataSources.Select(ds =>
            $"<DataBand Name=\"Band_{ds}\" DataSource=\"{ds}\" />"));

        var extra = includeConnectionString
            ? "\n  <Connection ConnectionString=\"Data Source=myserver;Initial Catalog=mydb\" />"
            : "";

        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Report>
              <ReportPage Name="Page1">
                {dataBands}
              </ReportPage>{extra}
            </Report>
            """;

        File.WriteAllText(Path.Combine(dir, $"{reportName}.frx"), xml);
    }

    private static void CreateFrxEmpty(string dir, string reportName)
    {
        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Report>
              <ReportPage Name="Page1">
              </ReportPage>
            </Report>
            """;

        File.WriteAllText(Path.Combine(dir, $"{reportName}.frx"), xml);
    }

    private static void CreateQueriesXml(string dir, IEnumerable<string> queryNames)
    {
        var queries = string.Join("\n    ", queryNames.Select(n =>
            $"<Query Name=\"{n}\"><SQL>SELECT * FROM Foo WHERE Name = '{n}'</SQL></Query>"));

        var xml = $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Queries>
              {queries}
            </Queries>
            """;

        File.WriteAllText(Path.Combine(dir, "Queries.xml"), xml);
    }

    #endregion

    #region Test 1: Valid report — no errors

    [Fact]
    public async Task ValidateReport_CorrectReport_NoIssues()
    {
        // Arrange: .frx references exactly the queries defined in Queries.xml
        var dir = CreateReportDir("CorrectReport");
        CreateFrx(dir, "CorrectReport", new[] { "MainData", "Totals" });
        CreateQueriesXml(dir, new[] { "MainData", "Totals" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("Проблем не обнаружено", result);
        Assert.DoesNotContain("[FAIL]", result);
        Assert.DoesNotContain("[WARN]", result);
    }

    #endregion

    #region Test 2: Dataset mismatch

    [Fact]
    public async Task ValidateReport_DataSourceNotInQueries_ReportsFailure()
    {
        // Arrange: .frx references "MissingQuery" which is absent from Queries.xml
        var dir = CreateReportDir("MismatchReport");
        CreateFrx(dir, "MismatchReport", new[] { "ExistingQuery", "MissingQuery" });
        CreateQueriesXml(dir, new[] { "ExistingQuery" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("[FAIL]", result);
        Assert.Contains("Несовпадение датасетов", result);
        Assert.Contains("MissingQuery", result);
    }

    #endregion

    #region Test 3: Hardcoded connection strings

    [Fact]
    public async Task ValidateReport_HardcodedConnectionString_ReportsFailure()
    {
        // Arrange: .frx contains a hardcoded connection string
        var dir = CreateReportDir("HardcodedConnReport");
        CreateFrx(dir, "HardcodedConnReport", new[] { "MainData" }, includeConnectionString: true);
        CreateQueriesXml(dir, new[] { "MainData" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("[FAIL]", result);
        Assert.Contains("Хардкоженные строки подключения", result);
        Assert.Contains("ConnectionString", result);
    }

    [Fact]
    public async Task ValidateReport_DataSourcePattern_ReportsFailure()
    {
        // Arrange: .frx has "Data Source=" in content
        var dir = CreateReportDir("DataSourceReport");
        var frxXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <Report>
              <Connection DataSource="Data Source=sqlserver01;Initial Catalog=drx" />
              <ReportPage Name="Page1">
                <DataBand Name="Band1" DataSource="MainData" />
              </ReportPage>
            </Report>
            """;
        File.WriteAllText(Path.Combine(dir, "DataSourceReport.frx"), frxXml);
        CreateQueriesXml(dir, new[] { "MainData" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("[FAIL]", result);
        Assert.Contains("Хардкоженные строки подключения", result);
    }

    #endregion

    #region Test 4: Unused queries

    [Fact]
    public async Task ValidateReport_UnusedQueryInQueriesXml_ReportsWarning()
    {
        // Arrange: Queries.xml has "UnusedQuery" that no DataBand references
        var dir = CreateReportDir("UnusedQueryReport");
        CreateFrx(dir, "UnusedQueryReport", new[] { "MainData" });
        CreateQueriesXml(dir, new[] { "MainData", "UnusedQuery" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("[WARN]", result);
        Assert.Contains("Неиспользуемые запросы", result);
        Assert.Contains("UnusedQuery", result);
    }

    #endregion

    #region Test 5: Empty template

    [Fact]
    public async Task ValidateReport_EmptyFrxTemplate_ReportsWarning()
    {
        // Arrange: .frx exists but has no DataBand elements
        var dir = CreateReportDir("EmptyTemplateReport");
        CreateFrxEmpty(dir, "EmptyTemplateReport");
        CreateQueriesXml(dir, new[] { "SomeQuery" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("[WARN]", result);
        Assert.Contains("Пустой шаблон", result);
        Assert.Contains("DataBand", result);
    }

    #endregion

    #region Test 6: Non-existent path

    [Fact]
    public async Task ValidateReport_NonExistentPath_ReturnsError()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_tempDir, "does_not_exist");

        // Act
        var result = await _tool.ValidateReport(nonExistentPath);

        // Assert
        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найден", result);
    }

    #endregion

    #region Test 7: Directory with no .frx files

    [Fact]
    public async Task ValidateReport_NoDotFrxFiles_ReturnsNoFilesMessage()
    {
        // Arrange: directory exists but contains no .frx files
        var dir = CreateReportDir("NoFrxDir");
        File.WriteAllText(Path.Combine(dir, "readme.txt"), "placeholder");

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert
        Assert.Contains("не найдено файлов .frx", result);
    }

    #endregion

    #region Test 8: Report without Queries.xml

    [Fact]
    public async Task ValidateReport_NoQueriesXml_ReportsWarning()
    {
        // Arrange: .frx file exists with DataBands but no Queries.xml
        var dir = CreateReportDir("NoQueriesXmlReport");
        CreateFrx(dir, "NoQueriesXmlReport", new[] { "SomeData" });
        // intentionally not creating Queries.xml

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert: should warn that Queries.xml is absent but DataBand has data sources
        Assert.Contains("Queries.xml", result);
        Assert.Contains("[WARN]", result);
    }

    #endregion

    #region Additional edge-case tests

    [Fact]
    public async Task ValidateReport_PathOutsideAllowed_ReturnsDenyMessage()
    {
        // Arrange: temporarily remove SOLUTION_PATH to simulate access denial
        Environment.SetEnvironmentVariable("SOLUTION_PATH", "");

        try
        {
            // Act
            var result = await _tool.ValidateReport(_tempDir);

            // Assert
            Assert.Contains("ОШИБКА", result);
            Assert.Contains("Доступ запрещён", result);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        }
    }

    [Fact]
    public async Task ValidateReport_MultipleReports_AllChecked()
    {
        // Arrange: two report subdirectories — one clean, one with issues
        var cleanDir = CreateReportDir("CleanReport");
        CreateFrx(cleanDir, "CleanReport", new[] { "Data" });
        CreateQueriesXml(cleanDir, new[] { "Data" });

        var badDir = CreateReportDir("BadReport");
        CreateFrx(badDir, "BadReport", new[] { "MissingDs" });
        CreateQueriesXml(badDir, new[] { "AnotherQuery" });

        // Act
        var result = await _tool.ValidateReport(_tempDir);

        // Assert: both reports appear, one has issues
        Assert.Contains("CleanReport", result);
        Assert.Contains("BadReport", result);
        Assert.Contains("[FAIL]", result);
    }

    #endregion
}
