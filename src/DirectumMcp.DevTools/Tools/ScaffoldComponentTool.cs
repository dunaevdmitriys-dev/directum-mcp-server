using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldComponentTool
{
    [McpServerTool(Name = "scaffold_component")]
    [Description("Создание нового Remote Component (стороннего контрола) Directum RX: генерация полной структуры проекта с webpack, manifest, loaders, i18n и заготовками контролов.")]
    public async Task<string> ScaffoldComponent(
        [Description("Путь к директории, где будет создан проект (должна быть пустой или не существовать)")] string outputPath,
        [Description("Имя вендора (латиницей, например 'DirRX')")] string vendorName,
        [Description("Имя компонента (латиницей, например 'CRMComponents')")] string componentName,
        [Description("Версия компонента (например '1.0')")] string version = "1.0",
        [Description("Список контролов через запятую: 'Имя:Scope' (Scope = Cover или Card). Пример: 'Dashboard:Cover,CardView:Card'")] string controls = "MyControl:Cover")
    {
        if (string.IsNullOrWhiteSpace(outputPath))
            return "**ОШИБКА**: Параметр `outputPath` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(vendorName))
            return "**ОШИБКА**: Параметр `vendorName` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(componentName))
            return "**ОШИБКА**: Параметр `componentName` не может быть пустым.";

        if (!PathGuard.IsAllowed(outputPath))
            return PathGuard.DenyMessage(outputPath);

        if (Directory.Exists(outputPath) && Directory.GetFileSystemEntries(outputPath).Length > 0)
            return $"**ОШИБКА**: Директория `{outputPath}` не пуста. Укажите пустую или несуществующую директорию.";

        var parsedControls = ParseControls(controls);
        if (parsedControls.Count == 0)
            return "**ОШИБКА**: Не удалось распознать контролы. Формат: 'Имя:Scope,Имя:Scope' (Scope = Cover или Card).";

        foreach (var c in parsedControls)
        {
            if (c.Scope is not ("Cover" and not "Card"))
            {
                if (c.Scope != "Cover" && c.Scope != "Card")
                    return $"**ОШИБКА**: Неизвестный scope `{c.Scope}` для контрола `{c.Name}`. Допустимые: Cover, Card.";
            }
        }

        var publicName = $"{vendorName}_{componentName}_{version.Replace(".", "_")}";
        var packageName = $"{vendorName}-{componentName}".ToLowerInvariant();

        Directory.CreateDirectory(outputPath);

        var createdFiles = new List<string>();

        // 1. package.json
        await WriteFileAsync(outputPath, "package.json", GeneratePackageJson(packageName, version));
        createdFiles.Add("package.json");

        // 2. webpack.config.js
        await WriteFileAsync(outputPath, "webpack.config.js", GenerateWebpackConfig(publicName));
        createdFiles.Add("webpack.config.js");

        // 3. component.manifest.js
        await WriteFileAsync(outputPath, "component.manifest.js",
            GenerateManifest(vendorName, componentName, version, parsedControls));
        createdFiles.Add("component.manifest.js");

        // 4. component.loaders.ts
        await WriteFileAsync(outputPath, "component.loaders.ts", GenerateLoaders(parsedControls));
        createdFiles.Add("component.loaders.ts");

        // 5. index.js
        await WriteFileAsync(outputPath, "index.js", "// Entry point — not used directly, Module Federation exposes loaders\n");
        createdFiles.Add("index.js");

        // 6. index.html (for dev server)
        await WriteFileAsync(outputPath, "index.html", GenerateIndexHtml(componentName));
        createdFiles.Add("index.html");

        // 7. public-path.js
        await WriteFileAsync(outputPath, "public-path.js",
            "// Dynamic public path for remote assets\n__webpack_public_path__ = document.currentScript?.src?.replace(/[^/]+$/, '') ?? '/';\n");
        createdFiles.Add("public-path.js");

        // 8. i18n.js
        await WriteFileAsync(outputPath, "i18n.js", GenerateI18nInit());
        createdFiles.Add("i18n.js");

        // 9. host-api-stub.ts
        await WriteFileAsync(outputPath, "host-api-stub.ts", GenerateHostApiStub());
        createdFiles.Add("host-api-stub.ts");

        // 10. host-context-stub.ts
        await WriteFileAsync(outputPath, "host-context-stub.ts", GenerateHostContextStub());
        createdFiles.Add("host-context-stub.ts");

        // 11. tsconfig.json
        await WriteFileAsync(outputPath, "tsconfig.json", GenerateTsConfig());
        createdFiles.Add("tsconfig.json");

        // 12. .gitignore
        await WriteFileAsync(outputPath, ".gitignore", "node_modules/\ndist/\n*.log\n");
        createdFiles.Add(".gitignore");

        // 13. Locales
        var ruTranslation = GenerateTranslationJson(parsedControls, "ru");
        var enTranslation = GenerateTranslationJson(parsedControls, "en");
        await WriteFileAsync(outputPath, "locales/ru/translation.json", ruTranslation);
        await WriteFileAsync(outputPath, "locales/en/translation.json", enTranslation);
        createdFiles.Add("locales/ru/translation.json");
        createdFiles.Add("locales/en/translation.json");

        // 14. Control sources and loaders
        foreach (var control in parsedControls)
        {
            var kebabName = ToKebabCase(control.Name);

            // src/controls/{name}/{Name}.tsx
            var controlContent = GenerateControlComponent(control.Name, control.Scope);
            await WriteFileAsync(outputPath, $"src/controls/{kebabName}/{control.Name}.tsx", controlContent);
            createdFiles.Add($"src/controls/{kebabName}/{control.Name}.tsx");

            // src/controls/{name}/index.ts
            await WriteFileAsync(outputPath, $"src/controls/{kebabName}/index.ts",
                $"export {{ {control.Name} }} from './{control.Name}';\n");
            createdFiles.Add($"src/controls/{kebabName}/index.ts");

            // src/loaders/{name}-loader.tsx
            var loaderContent = GenerateLoader(control.Name, kebabName, control.Scope);
            await WriteFileAsync(outputPath, $"src/loaders/{kebabName}-loader.tsx", loaderContent);
            createdFiles.Add($"src/loaders/{kebabName}-loader.tsx");
        }

        // 15. src/shared/api.ts
        await WriteFileAsync(outputPath, "src/shared/api.ts",
            "// Shared API helpers for data fetching\n\nexport async function fetchData<T>(url: string): Promise<T> {\n  const response = await fetch(url);\n  if (!response.ok) throw new Error(`HTTP ${response.status}`);\n  return response.json();\n}\n");
        createdFiles.Add("src/shared/api.ts");

        // 16. src/shared/theme.ts
        await WriteFileAsync(outputPath, "src/shared/theme.ts", GenerateThemeHelper());
        createdFiles.Add("src/shared/theme.ts");

        // Build report
        var sb = new StringBuilder();
        sb.AppendLine("## Remote Component создан");
        sb.AppendLine();
        sb.AppendLine($"**Путь:** `{outputPath}`");
        sb.AppendLine($"**Вендор:** {vendorName}");
        sb.AppendLine($"**Компонент:** {componentName} v{version}");
        sb.AppendLine($"**Module Federation:** `{publicName}`");
        sb.AppendLine();

        sb.AppendLine($"### Контролы ({parsedControls.Count})");
        sb.AppendLine();
        sb.AppendLine("| Контрол | Scope | Loader | Исходник |");
        sb.AppendLine("|---------|-------|--------|----------|");
        foreach (var c in parsedControls)
        {
            var kebab = ToKebabCase(c.Name);
            sb.AppendLine($"| {c.Name} | {c.Scope} | {kebab}-loader | src/controls/{kebab}/ |");
        }
        sb.AppendLine();

        sb.AppendLine($"### Создано файлов: {createdFiles.Count}");
        sb.AppendLine();
        foreach (var f in createdFiles)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine("```bash");
        sb.AppendLine($"cd \"{outputPath}\"");
        sb.AppendLine("npm install");
        sb.AppendLine("npm run build");
        sb.AppendLine("```");
        sb.AppendLine();
        sb.AppendLine("После сборки скопируйте содержимое `dist/` в директорию компонентов модуля.");

        return sb.ToString();
    }

    #region Generators

    private static string GeneratePackageJson(string name, string version)
    {
        return $$"""
            {
              "name": "{{name}}",
              "version": "{{version}}.0",
              "private": true,
              "scripts": {
                "build": "webpack --mode production",
                "build:dev": "webpack --mode development",
                "start": "webpack serve --mode development --open"
              },
              "dependencies": {
                "react": "^18.2.0",
                "react-dom": "^18.2.0",
                "i18next": "^23.7.0",
                "react-i18next": "^13.5.0"
              },
              "devDependencies": {
                "@directum/sungero-remote-component-types": "1.0.1",
                "@directum/sungero-remote-component-metadata-plugin": "1.0.1",
                "@babel/core": "^7.24.0",
                "@babel/preset-react": "^7.23.0",
                "@babel/preset-typescript": "^7.23.0",
                "babel-loader": "^9.1.3",
                "css-loader": "^6.10.0",
                "style-loader": "^3.3.4",
                "html-webpack-plugin": "^5.6.0",
                "typescript": "^5.3.0",
                "webpack": "^5.90.0",
                "webpack-cli": "^5.1.4",
                "webpack-dev-server": "^5.0.0"
              }
            }
            """;
    }

    private static string GenerateWebpackConfig(string publicName)
    {
        return $$"""
            const path = require('path');
            const webpack = require('webpack');
            const HtmlWebpackPlugin = require('html-webpack-plugin');
            const manifest = require('./component.manifest');
            const SungeroRemoteComponentMetadataPlugin =
              require('@directum/sungero-remote-component-metadata-plugin');

            const isProduction = process.env.NODE_ENV === 'production';

            module.exports = {
              entry: {
                index: './index.js',
                '{{publicName}}': './public-path.js',
              },
              output: {
                path: path.resolve(__dirname, 'dist'),
                publicPath: 'auto',
                filename: isProduction ? '[name].[contenthash].js' : '[name].js',
                clean: true,
              },
              resolve: {
                extensions: ['.ts', '.tsx', '.js', '.jsx'],
              },
              module: {
                rules: [
                  {
                    test: /\.[jt]sx?$/,
                    exclude: /node_modules/,
                    use: {
                      loader: 'babel-loader',
                      options: {
                        presets: [
                          '@babel/preset-react',
                          '@babel/preset-typescript',
                        ],
                      },
                    },
                  },
                  {
                    test: /\.css$/,
                    use: ['style-loader', 'css-loader'],
                  },
                ],
              },
              plugins: [
                new webpack.container.ModuleFederationPlugin({
                  name: '{{publicName}}',
                  filename: 'remoteEntry.js',
                  exposes: {
                    loaders: './component.loaders',
                    publicPath: './public-path',
                  },
                  shared: {
                    react: { singleton: true, requiredVersion: false },
                    'react-dom': { singleton: true, requiredVersion: false },
                    i18next: { singleton: true, requiredVersion: false },
                    'react-i18next': { singleton: true, requiredVersion: false },
                  },
                }),
                new SungeroRemoteComponentMetadataPlugin(manifest),
                ...(isProduction ? [] : [
                  new HtmlWebpackPlugin({ template: './index.html' }),
                ]),
              ],
              devServer: {
                port: 3001,
                hot: true,
                headers: { 'Access-Control-Allow-Origin': '*' },
              },
            };
            """;
    }

    private static string GenerateManifest(string vendorName, string componentName, string version,
        List<ControlDef> controls)
    {
        var sb = new StringBuilder();
        sb.AppendLine("module.exports = {");
        sb.AppendLine($"  vendorName: '{vendorName}',");
        sb.AppendLine($"  componentName: '{componentName}',");
        sb.AppendLine($"  componentVersion: '{version}',");
        sb.AppendLine($"  publicName: '{vendorName}_{componentName}_{version.Replace(".", "_")}',");
        sb.AppendLine("  hostApiVersion: '1.0.1',");
        sb.AppendLine("  controls: [");

        for (int i = 0; i < controls.Count; i++)
        {
            var c = controls[i];
            var kebab = ToKebabCase(c.Name);
            var comma = i < controls.Count - 1 ? "," : "";
            sb.AppendLine("    {");
            sb.AppendLine($"      name: '{c.Name}',");
            sb.AppendLine($"      loaderName: '{kebab}-loader',");
            sb.AppendLine($"      scope: '{c.Scope}',");
            sb.AppendLine("      displayName: {");
            sb.AppendLine($"        ru: '{c.Name}',");
            sb.AppendLine($"        en: '{c.Name}',");
            sb.AppendLine("      },");
            sb.AppendLine($"    }}{comma}");
        }

        sb.AppendLine("  ],");
        sb.AppendLine("};");
        return sb.ToString();
    }

    private static string GenerateLoaders(List<ControlDef> controls)
    {
        var sb = new StringBuilder();
        foreach (var c in controls)
        {
            var kebab = ToKebabCase(c.Name);
            var camel = char.ToLowerInvariant(c.Name[0]) + c.Name[1..];
            sb.AppendLine($"import {{ {camel}Loader }} from './src/loaders/{kebab}-loader';");
        }
        sb.AppendLine();
        sb.Append("export { ");
        sb.Append(string.Join(", ", controls.Select(c =>
            char.ToLowerInvariant(c.Name[0]) + c.Name[1..] + "Loader")));
        sb.AppendLine(" };");
        return sb.ToString();
    }

    private static string GenerateControlComponent(string name, string scope)
    {
        var apiType = scope == "Card" ? "IRemoteComponentCardApi" : "IRemoteComponentCoverApi";
        return $$"""
            import React from 'react';
            import { useTranslation } from 'react-i18next';

            interface {{name}}Props {
              api: any;
              context: any;
            }

            export const {{name}}: React.FC<{{name}}Props> = ({ api, context }) => {
              const { t } = useTranslation();

              return (
                <div className="{{ToKebabCase(name)}}">
                  <h2>{t('{{char.ToLowerInvariant(name[0]) + name[1..]}}.title', '{{name}}')}</h2>
                  <p>{t('{{char.ToLowerInvariant(name[0]) + name[1..]}}.description', 'Описание контрола')}</p>
                </div>
              );
            };
            """;
    }

    private static string GenerateLoader(string name, string kebabName, string scope)
    {
        var camel = char.ToLowerInvariant(name[0]) + name[1..];
        var apiType = scope == "Card" ? "ILoaderArgs" : "ILoaderArgs";
        return $$"""
            import React from 'react';
            import ReactDOM from 'react-dom/client';
            import { {{name}} } from '../controls/{{kebabName}}';
            import '../../../i18n';

            interface ILoaderArgs {
              container: HTMLElement;
              api: any;
              context: any;
              controlInfo: any;
            }

            export const {{camel}}Loader = (args: ILoaderArgs) => {
              const { container, api, context } = args;
              const root = ReactDOM.createRoot(container);

              root.render(
                <React.StrictMode>
                  <{{name}} api={api} context={context} />
                </React.StrictMode>
              );

              return () => {
                root.unmount();
              };
            };
            """;
    }

    private static string GenerateIndexHtml(string componentName)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="ru">
            <head>
              <meta charset="UTF-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1.0" />
              <title>{{componentName}} — Dev</title>
              <style>
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 20px; }
                #root { border: 1px dashed #ccc; padding: 20px; min-height: 200px; }
              </style>
            </head>
            <body>
              <h1>{{componentName}} — Development</h1>
              <div id="root"></div>
            </body>
            </html>
            """;
    }

    private static string GenerateI18nInit()
    {
        return """
            import i18n from 'i18next';
            import { initReactI18next } from 'react-i18next';
            import ru from './locales/ru/translation.json';
            import en from './locales/en/translation.json';

            if (!i18n.isInitialized) {
              i18n.use(initReactI18next).init({
                resources: {
                  ru: { translation: ru },
                  en: { translation: en },
                },
                lng: 'ru',
                fallbackLng: 'en',
                interpolation: { escapeValue: false },
              });
            }

            export default i18n;
            """;
    }

    private static string GenerateHostApiStub()
    {
        return """
            // Stub for local development without Directum RX
            // Implements IRemoteComponentCardApi and IRemoteComponentCoverApi

            export const cardApiStub = {
              executeAction: async (actionName: string) => {
                console.log(`[stub] executeAction: ${actionName}`);
              },
              canExecuteAction: (actionName: string) => {
                console.log(`[stub] canExecuteAction: ${actionName}`);
                return true;
              },
              getEntity: <T>() => ({} as T),
              onControlUpdate: undefined,
            };

            export const coverApiStub = {
              getActionsMetadata: () => [],
              executeAction: async (actionId: string) => {
                console.log(`[stub] executeAction: ${actionId}`);
              },
              onControlUpdate: undefined,
            };
            """;
    }

    private static string GenerateHostContextStub()
    {
        return """
            // Stub context for local development

            export const contextStub = {
              userId: 1,
              currentCulture: 'ru',
              tenant: {},
              theme: 'Light' as const,
              clientId: 'dev-client',
              logger: {
                info: console.log,
                warn: console.warn,
                error: console.error,
              },
              moduleLicenses: [],
            };
            """;
    }

    private static string GenerateTsConfig()
    {
        return """
            {
              "compilerOptions": {
                "target": "ES2020",
                "module": "ESNext",
                "moduleResolution": "node",
                "jsx": "react-jsx",
                "strict": true,
                "esModuleInterop": true,
                "skipLibCheck": true,
                "forceConsistentCasingInFileNames": true,
                "resolveJsonModule": true,
                "declaration": false,
                "outDir": "./dist",
                "baseUrl": ".",
                "paths": {
                  "@controls/*": ["src/controls/*"],
                  "@shared/*": ["src/shared/*"]
                }
              },
              "include": ["src/**/*", "*.ts"],
              "exclude": ["node_modules", "dist"]
            }
            """;
    }

    private static string GenerateThemeHelper()
    {
        return """
            // Apply Directum RX theme CSS variables to component root

            type Theme = 'Light' | 'Dark';

            const themeVariables: Record<Theme, Record<string, string>> = {
              Light: {
                '--bg-primary': '#ffffff',
                '--bg-secondary': '#f5f5f5',
                '--text-primary': '#333333',
                '--text-secondary': '#666666',
                '--border-color': '#e0e0e0',
                '--accent-color': '#0066cc',
              },
              Dark: {
                '--bg-primary': '#1e1e1e',
                '--bg-secondary': '#2d2d2d',
                '--text-primary': '#e0e0e0',
                '--text-secondary': '#a0a0a0',
                '--border-color': '#444444',
                '--accent-color': '#4da6ff',
              },
            };

            export function applyTheme(element: HTMLElement, theme: Theme): void {
              const vars = themeVariables[theme] || themeVariables.Light;
              for (const [key, value] of Object.entries(vars)) {
                element.style.setProperty(key, value);
              }
            }
            """;
    }

    private static string GenerateTranslationJson(List<ControlDef> controls, string lang)
    {
        var entries = new Dictionary<string, object>();
        foreach (var c in controls)
        {
            var key = char.ToLowerInvariant(c.Name[0]) + c.Name[1..];
            var title = lang == "ru" ? c.Name : c.Name;
            var desc = lang == "ru" ? "Описание контрола" : "Control description";
            entries[key] = new Dictionary<string, string>
            {
                ["title"] = title,
                ["description"] = desc
            };
        }
        return JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
    }

    #endregion

    #region Helpers

    private static List<ControlDef> ParseControls(string controls)
    {
        var result = new List<ControlDef>();
        foreach (var part in controls.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var segments = part.Split(':', StringSplitOptions.TrimEntries);
            var name = segments[0];
            var scope = segments.Length > 1 ? segments[1] : "Cover";

            if (!string.IsNullOrEmpty(name))
                result.Add(new ControlDef(name, scope));
        }
        return result;
    }

    private static string ToKebabCase(string name)
    {
        var result = new StringBuilder();
        for (int i = 0; i < name.Length; i++)
        {
            var ch = name[i];
            if (char.IsUpper(ch) && i > 0)
            {
                result.Append('-');
                result.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                result.Append(char.ToLowerInvariant(ch));
            }
        }
        return result.ToString();
    }

    private static async Task WriteFileAsync(string root, string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath);
        var dir = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(fullPath, content);
    }

    private record ControlDef(string Name, string Scope);

    #endregion
}
