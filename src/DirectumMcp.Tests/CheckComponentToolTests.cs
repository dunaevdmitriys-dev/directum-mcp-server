using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class CheckComponentToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly CheckComponentTool _tool;

    private const string ValidPackageJson = """
        {
          "name": "my-component",
          "version": "1.0.0",
          "scripts": { "build": "webpack --mode production" },
          "dependencies": {
            "react": "^18.2.0",
            "react-dom": "^18.2.0"
          },
          "devDependencies": {
            "@directum/sungero-remote-component-types": "1.0.1",
            "@directum/sungero-remote-component-metadata-plugin": "1.0.1",
            "webpack": "^5.90.0",
            "webpack-cli": "^5.1.4"
          }
        }
        """;

    private const string ValidWebpackConfig = """
        const webpack = require('webpack');
        const SungeroRemoteComponentMetadataPlugin = require('@directum/sungero-remote-component-metadata-plugin');
        module.exports = {
          output: { publicPath: 'auto' },
          plugins: [
            new webpack.container.ModuleFederationPlugin({
              name: 'MyVendor_MyComponent_1_0',
              filename: 'remoteEntry.js',
              exposes: { loaders: './component.loaders' },
              shared: { react: { singleton: true }, 'react-dom': { singleton: true } }
            }),
            new SungeroRemoteComponentMetadataPlugin(manifest)
          ]
        };
        """;

    private const string ValidManifest = """
        module.exports = {
          vendorName: 'MyVendor',
          componentName: 'MyComponent',
          controls: [
            { name: 'Dashboard', loaderName: 'dashboard-loader', scope: 'Cover' },
            { name: 'CardView', loaderName: 'card-view-loader', scope: 'Card' }
          ]
        };
        """;

    private const string ValidLoaders = """
        import { dashboardLoader } from './src/loaders/dashboard-loader';
        import { cardViewLoader } from './src/loaders/card-view-loader';
        export { dashboardLoader, cardViewLoader };
        """;

    public CheckComponentToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CheckCompTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new CheckComponentTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string CreateComponentDir(string name)
    {
        var dir = Path.Combine(_tempDir, name);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFile(string dir, string relativePath, string content)
    {
        var fullPath = Path.Combine(dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private string SetupFullComponent(string name)
    {
        var dir = CreateComponentDir(name);
        WriteFile(dir, "package.json", ValidPackageJson);
        WriteFile(dir, "webpack.config.js", ValidWebpackConfig);
        WriteFile(dir, "component.manifest.js", ValidManifest);
        WriteFile(dir, "component.loaders.ts", ValidLoaders);
        // Create source dirs
        Directory.CreateDirectory(Path.Combine(dir, "src", "controls", "dashboard"));
        Directory.CreateDirectory(Path.Combine(dir, "src", "controls", "card-view"));
        Directory.CreateDirectory(Path.Combine(dir, "src", "loaders"));
        WriteFile(dir, "src/loaders/dashboard-loader.tsx", "export const dashboardLoader = () => {};");
        WriteFile(dir, "src/loaders/card-view-loader.tsx", "export const cardViewLoader = () => {};");
        return dir;
    }

    #region Full Valid Component

    [Fact]
    public async Task Check_ValidComponent_NoIssues()
    {
        var dir = SetupFullComponent("valid_comp");
        // Create dist with remoteEntry.js and metadata.json (older than source to avoid rebuild warning)
        WriteFile(dir, "dist/remoteEntry.js", "// built");
        WriteFile(dir, "dist/metadata.json", "{}");
        // Make dist files newer than source
        var distEntry = Path.Combine(dir, "dist", "remoteEntry.js");
        File.SetLastWriteTime(distEntry, DateTime.Now.AddMinutes(1));

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("Контролы (2)", result);
        Assert.Contains("Dashboard", result);
        Assert.Contains("CardView", result);
        Assert.Contains("my-component", result);
        Assert.DoesNotContain("Критические", result);
    }

    #endregion

    #region Missing Files

    [Fact]
    public async Task Check_MissingPackageJson_ReportsCritical()
    {
        var dir = CreateComponentDir("no_pkg");
        WriteFile(dir, "webpack.config.js", ValidWebpackConfig);

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("package.json", result);
        Assert.Contains("Критические", result);
    }

    [Fact]
    public async Task Check_MissingWebpack_ReportsCritical()
    {
        var dir = CreateComponentDir("no_webpack");
        WriteFile(dir, "package.json", ValidPackageJson);

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("webpack.config.js", result);
        Assert.Contains("Критические", result);
    }

    [Fact]
    public async Task Check_MissingManifest_ReportsCritical()
    {
        var dir = CreateComponentDir("no_manifest");
        WriteFile(dir, "package.json", ValidPackageJson);
        WriteFile(dir, "webpack.config.js", ValidWebpackConfig);

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("component.manifest.js", result);
        Assert.Contains("Критические", result);
    }

    [Fact]
    public async Task Check_MissingLoaders_ReportsCritical()
    {
        var dir = CreateComponentDir("no_loaders");
        WriteFile(dir, "package.json", ValidPackageJson);
        WriteFile(dir, "webpack.config.js", ValidWebpackConfig);
        WriteFile(dir, "component.manifest.js", ValidManifest);

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("component.loaders", result);
        Assert.Contains("Критические", result);
    }

    #endregion

    #region Dependencies

    [Fact]
    public async Task Check_MissingReactDep_ReportsWarning()
    {
        var dir = CreateComponentDir("no_react");
        var pkgJson = """
            {
              "name": "test",
              "version": "1.0.0",
              "scripts": { "build": "webpack" },
              "dependencies": {},
              "devDependencies": {
                "@directum/sungero-remote-component-types": "1.0.1",
                "@directum/sungero-remote-component-metadata-plugin": "1.0.1",
                "webpack": "^5.90.0",
                "webpack-cli": "^5.1.4"
              }
            }
            """;
        WriteFile(dir, "package.json", pkgJson);
        WriteFile(dir, "webpack.config.js", ValidWebpackConfig);

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("react", result);
        Assert.Contains("Предупреждения", result);
    }

    #endregion

    #region Build Output

    [Fact]
    public async Task Check_NoDist_ReportsNotBuilt()
    {
        var dir = SetupFullComponent("no_dist");

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("Не собран", result);
    }

    [Fact]
    public async Task Check_DistWithoutRemoteEntry_ReportsCritical()
    {
        var dir = SetupFullComponent("no_remote_entry");
        Directory.CreateDirectory(Path.Combine(dir, "dist"));
        WriteFile(dir, "dist/other.js", "// something");

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("remoteEntry.js", result);
        Assert.Contains("Критические", result);
    }

    #endregion

    #region i18n

    [Fact]
    public async Task Check_WithI18n_ShowsI18nStatus()
    {
        var dir = SetupFullComponent("with_i18n");
        WriteFile(dir, "locales/ru/translation.json", "{}");
        WriteFile(dir, "locales/en/translation.json", "{}");

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("i18n: ✅", result);
    }

    [Fact]
    public async Task Check_MissingRuLocale_ReportsWarning()
    {
        var dir = SetupFullComponent("missing_ru");
        WriteFile(dir, "locales/en/translation.json", "{}");

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("русская локализация", result);
    }

    #endregion

    #region Controls Info

    [Fact]
    public async Task Check_ReportShowsControlsTable()
    {
        var dir = SetupFullComponent("controls_table");

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("| Dashboard |", result);
        Assert.Contains("| CardView |", result);
        Assert.Contains("Cover", result);
        Assert.Contains("Card", result);
    }

    [Fact]
    public async Task Check_ReportShowsSummary()
    {
        var dir = SetupFullComponent("summary");

        var result = await _tool.CheckComponent(dir);

        Assert.Contains("Итого", result);
        Assert.Contains("Контролов:", result);
        Assert.Contains("Зависимостей:", result);
    }

    #endregion

    #region Error Scenarios

    [Fact]
    public async Task Check_NonexistentPath_ReturnsError()
    {
        var result = await _tool.CheckComponent(Path.Combine(_tempDir, "no_such"));

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найдена", result);
    }

    [Fact]
    public async Task Check_PathOutsideSolutionPath_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "/usr";
        if (!Directory.Exists(outsidePath))
            return;

        var result = await _tool.CheckComponent(outsidePath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    #endregion
}
