using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ScaffoldComponentToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly ScaffoldComponentTool _tool;

    public ScaffoldComponentToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ScaffoldCompTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new ScaffoldComponentTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string GetOutputDir(string name) => Path.Combine(_tempDir, name);

    #region Basic Scaffolding

    [Fact]
    public async Task Scaffold_SingleControl_CreatesAllFiles()
    {
        var output = GetOutputDir("single");

        var result = await _tool.ScaffoldComponent(output, "MyVendor", "MyComponent",
            controls: "Dashboard:Cover");

        Assert.Contains("Remote Component создан", result);
        Assert.Contains("Dashboard", result);
        Assert.True(File.Exists(Path.Combine(output, "package.json")));
        Assert.True(File.Exists(Path.Combine(output, "webpack.config.js")));
        Assert.True(File.Exists(Path.Combine(output, "component.manifest.js")));
        Assert.True(File.Exists(Path.Combine(output, "component.loaders.ts")));
        Assert.True(File.Exists(Path.Combine(output, "tsconfig.json")));
        Assert.True(File.Exists(Path.Combine(output, ".gitignore")));
    }

    [Fact]
    public async Task Scaffold_MultipleControls_CreatesAllControlDirs()
    {
        var output = GetOutputDir("multi");

        var result = await _tool.ScaffoldComponent(output, "DirRX", "CRM",
            controls: "PipelineKanban:Cover,Customer360:Card,FunnelChart:Cover");

        Assert.Contains("Контролы (3)", result);
        Assert.True(Directory.Exists(Path.Combine(output, "src", "controls", "pipeline-kanban")));
        Assert.True(Directory.Exists(Path.Combine(output, "src", "controls", "customer360")));
        Assert.True(Directory.Exists(Path.Combine(output, "src", "controls", "funnel-chart")));
        Assert.True(File.Exists(Path.Combine(output, "src", "loaders", "pipeline-kanban-loader.tsx")));
        Assert.True(File.Exists(Path.Combine(output, "src", "loaders", "customer360-loader.tsx")));
        Assert.True(File.Exists(Path.Combine(output, "src", "loaders", "funnel-chart-loader.tsx")));
    }

    #endregion

    #region File Content

    [Fact]
    public async Task Scaffold_PackageJson_ContainsCorrectDeps()
    {
        var output = GetOutputDir("deps");
        await _tool.ScaffoldComponent(output, "V", "C", controls: "X:Cover");

        var content = File.ReadAllText(Path.Combine(output, "package.json"));

        Assert.Contains("react", content);
        Assert.Contains("react-dom", content);
        Assert.Contains("webpack", content);
        Assert.Contains("@directum/sungero-remote-component-types", content);
        Assert.Contains("@directum/sungero-remote-component-metadata-plugin", content);
    }

    [Fact]
    public async Task Scaffold_WebpackConfig_HasModuleFederation()
    {
        var output = GetOutputDir("webpack");
        await _tool.ScaffoldComponent(output, "V", "C", controls: "X:Cover");

        var content = File.ReadAllText(Path.Combine(output, "webpack.config.js"));

        Assert.Contains("ModuleFederationPlugin", content);
        Assert.Contains("remoteEntry.js", content);
        Assert.Contains("singleton: true", content);
        Assert.Contains("SungeroRemoteComponentMetadataPlugin", content);
    }

    [Fact]
    public async Task Scaffold_Manifest_ContainsControlsAndVendor()
    {
        var output = GetOutputDir("manifest");
        await _tool.ScaffoldComponent(output, "TestVendor", "TestComp",
            controls: "MyWidget:Cover");

        var content = File.ReadAllText(Path.Combine(output, "component.manifest.js"));

        Assert.Contains("vendorName: 'TestVendor'", content);
        Assert.Contains("componentName: 'TestComp'", content);
        Assert.Contains("name: 'MyWidget'", content);
        Assert.Contains("loaderName: 'my-widget-loader'", content);
        Assert.Contains("scope: 'Cover'", content);
    }

    [Fact]
    public async Task Scaffold_Loaders_ExportsAllControls()
    {
        var output = GetOutputDir("loaders");
        await _tool.ScaffoldComponent(output, "V", "C",
            controls: "Alpha:Cover,Beta:Card");

        var content = File.ReadAllText(Path.Combine(output, "component.loaders.ts"));

        Assert.Contains("alphaLoader", content);
        Assert.Contains("betaLoader", content);
        Assert.Contains("export", content);
    }

    [Fact]
    public async Task Scaffold_ControlComponent_HasReactStructure()
    {
        var output = GetOutputDir("react");
        await _tool.ScaffoldComponent(output, "V", "C", controls: "MyPanel:Cover");

        var content = File.ReadAllText(Path.Combine(output, "src", "controls", "my-panel", "MyPanel.tsx"));

        Assert.Contains("import React", content);
        Assert.Contains("useTranslation", content);
        Assert.Contains("MyPanel", content);
        Assert.Contains("React.FC", content);
    }

    #endregion

    #region i18n

    [Fact]
    public async Task Scaffold_CreatesI18nFiles()
    {
        var output = GetOutputDir("i18n");
        await _tool.ScaffoldComponent(output, "V", "C", controls: "Widget:Cover");

        Assert.True(File.Exists(Path.Combine(output, "locales", "ru", "translation.json")));
        Assert.True(File.Exists(Path.Combine(output, "locales", "en", "translation.json")));
        Assert.True(File.Exists(Path.Combine(output, "i18n.js")));
    }

    #endregion

    #region Stubs

    [Fact]
    public async Task Scaffold_CreatesHostStubs()
    {
        var output = GetOutputDir("stubs");
        await _tool.ScaffoldComponent(output, "V", "C", controls: "X:Cover");

        Assert.True(File.Exists(Path.Combine(output, "host-api-stub.ts")));
        Assert.True(File.Exists(Path.Combine(output, "host-context-stub.ts")));

        var apiStub = File.ReadAllText(Path.Combine(output, "host-api-stub.ts"));
        Assert.Contains("cardApiStub", apiStub);
        Assert.Contains("coverApiStub", apiStub);
    }

    #endregion

    #region Report

    [Fact]
    public async Task Scaffold_Report_ContainsNextSteps()
    {
        var output = GetOutputDir("report");
        var result = await _tool.ScaffoldComponent(output, "V", "C", controls: "X:Cover");

        Assert.Contains("Следующие шаги", result);
        Assert.Contains("npm install", result);
        Assert.Contains("npm run build", result);
    }

    [Fact]
    public async Task Scaffold_Report_ShowsFederationName()
    {
        var output = GetOutputDir("federation");
        var result = await _tool.ScaffoldComponent(output, "MyVendor", "MyComp", version: "2.0");

        Assert.Contains("MyVendor_MyComp_2_0", result);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Scaffold_NonEmptyDir_ReturnsError()
    {
        var output = GetOutputDir("notempty");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "existing.txt"), "data");

        var result = await _tool.ScaffoldComponent(output, "V", "C");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не пуста", result);
    }

    [Fact]
    public async Task Scaffold_EmptyVendor_ReturnsError()
    {
        var output = GetOutputDir("novendor");

        var result = await _tool.ScaffoldComponent(output, "", "C");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("vendorName", result);
    }

    [Fact]
    public async Task Scaffold_InvalidScope_ReturnsError()
    {
        var output = GetOutputDir("badscope");

        var result = await _tool.ScaffoldComponent(output, "V", "C", controls: "X:Invalid");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("Invalid", result);
    }

    [Fact]
    public async Task Scaffold_PathOutsideSolutionPath_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr";
        if (!Directory.Exists(outsidePath))
            return;

        var result = await _tool.ScaffoldComponent(
            Path.Combine(outsidePath, "test_comp"), "V", "C");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion
}
