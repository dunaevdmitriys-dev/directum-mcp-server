using System.ComponentModel;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Parsers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ScaffoldSpaTool
{
    private static readonly Dictionary<string, string> MtdTypeToTs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StringProperty"] = "string",
        ["IntegerProperty"] = "number",
        ["DoubleProperty"] = "number",
        ["BooleanProperty"] = "boolean",
        ["DateTimeProperty"] = "string",
        ["NavigationProperty"] = "number",
        ["EnumProperty"] = "string",
        ["TextProperty"] = "string",
    };

    private static readonly Dictionary<string, string> MtdTypeToFormComponent = new(StringComparer.OrdinalIgnoreCase)
    {
        ["StringProperty"] = "Input",
        ["IntegerProperty"] = "InputNumber",
        ["DoubleProperty"] = "InputNumber",
        ["BooleanProperty"] = "Switch",
        ["DateTimeProperty"] = "DatePicker",
        ["NavigationProperty"] = "Select",
        ["EnumProperty"] = "Select",
        ["TextProperty"] = "TextArea",
    };

    [McpServerTool(Name = "scaffold_spa")]
    [Description("Генерирует полный React SPA проект (Vite + TypeScript + Ant Design + HashRouter) для набора сущностей Directum RX. Выводит весь исходный код файлов в Markdown.")]
    public async Task<string> Execute(
        [Description("Путь к директории модуля с MTD-файлами сущностей (например work/DirRX.CRMSales)")] string modulePath,
        [Description("Путь для SPA проекта (например work/DirRX.CRM/crm-spa)")] string outputPath,
        [Description("Название приложения (например CRM)")] string appName,
        [Description("Список сущностей через запятую (например Deal,Pipeline,Stage,Lead)")] string entities)
    {
        if (string.IsNullOrWhiteSpace(modulePath))
            return "**ОШИБКА**: Параметр `modulePath` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(outputPath))
            return "**ОШИБКА**: Параметр `outputPath` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(appName))
            return "**ОШИБКА**: Параметр `appName` не может быть пустым.";
        if (string.IsNullOrWhiteSpace(entities))
            return "**ОШИБКА**: Параметр `entities` не может быть пустым.";

        if (!PathGuard.IsAllowed(modulePath))
            return PathGuard.DenyMessage(modulePath);

        if (!Directory.Exists(modulePath))
            return $"**ОШИБКА**: Директория модуля не найдена: `{modulePath}`";

        var entityNames = entities
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        if (entityNames.Count == 0)
            return "**ОШИБКА**: Не указано ни одной сущности.";

        // Parse entity MTDs
        var parsedEntities = new List<EntityInfo>();
        var errors = new List<string>();

        foreach (var name in entityNames)
        {
            var mtdPath = FindEntityMtd(modulePath, name);
            if (mtdPath == null)
            {
                errors.Add($"MTD файл для `{name}` не найден в `{modulePath}`");
                continue;
            }

            try
            {
                var info = await ParseEntityInfo(mtdPath, modulePath, name);
                parsedEntities.Add(info);
            }
            catch (Exception ex)
            {
                errors.Add($"Ошибка парсинга `{name}`: {ex.Message}");
            }
        }

        if (parsedEntities.Count == 0)
            return $"**ОШИБКА**: Не удалось разобрать ни одной сущности.\n\n" +
                   string.Join("\n", errors.Select(e => $"- {e}"));

        // Derive project name from outputPath
        var projectName = Path.GetFileName(outputPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var appNameLower = appName.ToLowerInvariant();

        // Generate all files
        var files = new List<GeneratedFile>();
        var fileIndex = 1;

        // 1. package.json
        files.Add(new GeneratedFile($"{fileIndex++}. package.json", "json",
            GeneratePackageJson(projectName)));

        // 2. vite.config.ts
        files.Add(new GeneratedFile($"{fileIndex++}. vite.config.ts", "typescript",
            GenerateViteConfig(appNameLower)));

        // 3. tsconfig.json
        files.Add(new GeneratedFile($"{fileIndex++}. tsconfig.json", "json",
            GenerateTsConfig()));

        // 4. tsconfig.node.json
        files.Add(new GeneratedFile($"{fileIndex++}. tsconfig.node.json", "json",
            GenerateTsConfigNode()));

        // 5. index.html
        files.Add(new GeneratedFile($"{fileIndex++}. index.html", "html",
            GenerateIndexHtml(appName)));

        // 6. config.js
        files.Add(new GeneratedFile($"{fileIndex++}. config.js", "javascript",
            GenerateConfigJs()));

        // 7. src/main.tsx
        files.Add(new GeneratedFile($"{fileIndex++}. src/main.tsx", "tsx",
            GenerateMainTsx(appName)));

        // 8. src/index.css
        files.Add(new GeneratedFile($"{fileIndex++}. src/index.css", "css",
            GenerateIndexCss()));

        // 9. src/vite-env.d.ts
        files.Add(new GeneratedFile($"{fileIndex++}. src/vite-env.d.ts", "typescript",
            GenerateViteEnvDts()));

        // 10. src/types/index.ts
        files.Add(new GeneratedFile($"{fileIndex++}. src/types/index.ts", "typescript",
            GenerateTypes(parsedEntities)));

        // 11. src/services/api.ts
        files.Add(new GeneratedFile($"{fileIndex++}. src/services/api.ts", "typescript",
            GenerateApiService(parsedEntities, appNameLower)));

        // 12. src/components/layout/AppLayout.tsx
        files.Add(new GeneratedFile($"{fileIndex++}. src/components/layout/AppLayout.tsx", "tsx",
            GenerateAppLayout()));

        // 13. src/components/layout/Sidebar.tsx
        files.Add(new GeneratedFile($"{fileIndex++}. src/components/layout/Sidebar.tsx", "tsx",
            GenerateSidebar(parsedEntities, appName)));

        // Entity pages
        foreach (var entity in parsedEntities)
        {
            files.Add(new GeneratedFile(
                $"{fileIndex++}. src/pages/{entity.Name}ListPage.tsx", "tsx",
                GenerateListPage(entity, appNameLower)));

            files.Add(new GeneratedFile(
                $"{fileIndex++}. src/pages/{entity.Name}DetailPage.tsx", "tsx",
                GenerateDetailPage(entity, appNameLower)));

            files.Add(new GeneratedFile(
                $"{fileIndex++}. src/pages/{entity.Name}FormPage.tsx", "tsx",
                GenerateFormPage(entity, appNameLower)));
        }

        // App.tsx (last, references all pages)
        files.Add(new GeneratedFile($"{fileIndex++}. src/App.tsx", "tsx",
            GenerateAppTsx(parsedEntities)));

        // Build markdown output
        var sb = new StringBuilder();
        sb.AppendLine($"## Scaffold SPA: {appName}");
        sb.AppendLine();

        if (errors.Count > 0)
        {
            sb.AppendLine("### Предупреждения");
            sb.AppendLine();
            foreach (var err in errors)
                sb.AppendLine($"- {err}");
            sb.AppendLine();
        }

        sb.AppendLine($"### Сущности ({parsedEntities.Count})");
        sb.AppendLine();
        sb.AppendLine("| Сущность | Свойств | Подпись |");
        sb.AppendLine("|----------|---------|--------|");
        foreach (var e in parsedEntities)
            sb.AppendLine($"| {e.Name} | {e.Properties.Count} | {e.DisplayName} |");
        sb.AppendLine();

        sb.AppendLine($"### Сгенерированные файлы ({files.Count})");
        sb.AppendLine();
        sb.AppendLine($"**Целевая директория:** `{outputPath}`");
        sb.AppendLine();

        foreach (var file in files)
        {
            sb.AppendLine($"#### {file.Title}");
            sb.AppendLine($"```{file.Language}");
            sb.AppendLine(file.Content);
            sb.AppendLine("```");
            sb.AppendLine();
        }

        sb.AppendLine("### Следующие шаги");
        sb.AppendLine();
        sb.AppendLine("1. Создайте файлы в указанной директории");
        sb.AppendLine("2. `cd " + outputPath + " && npm install && npm run dev`");
        sb.AppendLine("3. Настройте `config.js` с правильным `PUBLIC_API_PATH`");
        sb.AppendLine("4. Для продакшена: `npm run build` и настройте `directory_mapping` на IIS");

        return sb.ToString();
    }

    #region Entity Parsing

    private static string? FindEntityMtd(string modulePath, string entityName)
    {
        var candidates = Directory.GetFiles(modulePath, $"{entityName}.mtd", SearchOption.AllDirectories);
        return candidates
            .Where(f => !Path.GetFileName(f).Equals("Module.mtd", StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();
    }

    private static async Task<EntityInfo> ParseEntityInfo(string mtdPath, string modulePath, string entityName)
    {
        using var doc = await MtdParser.ParseRawAsync(mtdPath);
        var root = doc.RootElement;

        var name = GetString(root, "Name");
        if (string.IsNullOrEmpty(name))
            name = entityName;

        var guid = GetString(root, "NameGuid");

        var properties = new List<PropInfo>();

        if (root.TryGetProperty("Properties", out var propsEl) && propsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var prop in propsEl.EnumerateArray())
            {
                var propType = GetString(prop, "$type");
                if (propType.Contains("CollectionPropertyMetadata"))
                    continue;

                var isAncestor = prop.TryGetProperty("IsAncestorMetadata", out var ancEl)
                                 && ancEl.ValueKind == JsonValueKind.True;
                // Skip overridden ancestor properties unless they add something new
                if (isAncestor)
                {
                    // Include Name property from ancestors
                    var pName = GetString(prop, "Name");
                    if (!string.Equals(pName, "Name", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(pName, "Status", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                var propName = GetString(prop, "Name");
                var propCode = GetString(prop, "Code");
                if (string.IsNullOrEmpty(propCode))
                    propCode = propName;

                var shortType = ExtractShortType(propType);
                var tsType = MtdTypeToTs.GetValueOrDefault(shortType, "string");
                var formComponent = MtdTypeToFormComponent.GetValueOrDefault(shortType, "Input");
                var isRequired = prop.TryGetProperty("IsRequired", out var reqEl)
                                 && reqEl.ValueKind == JsonValueKind.True;

                string? navigationEntityGuid = null;
                if (shortType == "NavigationProperty" &&
                    prop.TryGetProperty("EntityGuid", out var egEl) &&
                    egEl.ValueKind == JsonValueKind.String)
                {
                    navigationEntityGuid = egEl.GetString();
                }

                properties.Add(new PropInfo(
                    propName, propCode, shortType, tsType,
                    formComponent, isRequired, navigationEntityGuid));
            }
        }

        // Try to read Russian labels from System.ru.resx
        var displayName = name;
        var propertyLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var resxRuPath = FindResxFile(mtdPath, name, isRussian: true);
        if (resxRuPath != null)
        {
            try
            {
                var resxEntries = await ResxParser.ParseAsync(resxRuPath);
                if (resxEntries.TryGetValue("DisplayName", out var dn) && !string.IsNullOrEmpty(dn))
                    displayName = dn;

                foreach (var (key, value) in resxEntries)
                {
                    if (key.StartsWith("Property_", StringComparison.Ordinal))
                    {
                        var propName = key["Property_".Length..];
                        propertyLabels[propName] = value;
                    }
                }
            }
            catch { /* resx read failed, use defaults */ }
        }

        // Apply labels to properties
        foreach (var prop in properties)
        {
            if (propertyLabels.TryGetValue(prop.Name, out var label))
                prop.Label = label;
            else
                prop.Label = prop.Name;
        }

        return new EntityInfo(name, guid, displayName, properties);
    }

    private static string? FindResxFile(string mtdPath, string entityName, bool isRussian)
    {
        var dir = Path.GetDirectoryName(mtdPath);
        if (dir == null) return null;

        var suffix = isRussian ? "System.ru.resx" : "System.resx";
        var fileName = $"{entityName}{suffix}";

        var candidate = Path.Combine(dir, fileName);
        if (File.Exists(candidate))
            return candidate;

        // Search in parent directories
        var parentDir = Path.GetDirectoryName(dir);
        if (parentDir != null)
        {
            var files = Directory.GetFiles(parentDir, fileName, SearchOption.AllDirectories);
            if (files.Length > 0)
                return files[0];
        }

        return null;
    }

    private static string ExtractShortType(string fullType)
    {
        var className = fullType.Split(',')[0].Split('.').LastOrDefault() ?? fullType;
        return className.Replace("Metadata", "");
    }

    private static string GetString(JsonElement el, string propertyName)
    {
        return el.TryGetProperty(propertyName, out var val) && val.ValueKind == JsonValueKind.String
            ? val.GetString() ?? ""
            : "";
    }

    #endregion

    #region Project Files Generation

    private static string GeneratePackageJson(string projectName)
    {
        return $$"""
{
  "name": "{{projectName}}",
  "version": "1.0.0",
  "private": true,
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "tsc && vite build",
    "preview": "vite preview"
  },
  "dependencies": {
    "react": "^18.2.0",
    "react-dom": "^18.2.0",
    "react-router-dom": "^6.20.0",
    "antd": "^5.12.0",
    "@ant-design/icons": "^5.2.0",
    "dayjs": "^1.11.0"
  },
  "devDependencies": {
    "@types/react": "^18.2.0",
    "@types/react-dom": "^18.2.0",
    "@vitejs/plugin-react": "^4.2.0",
    "typescript": "^5.3.0",
    "vite": "^5.0.0"
  }
}
""";
    }

    private static string GenerateViteConfig(string appNameLower)
    {
        return $$"""
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  base: '/Client/content/{{appNameLower}}/',
  build: {
    outDir: '../{{appNameLower}}_dist',
    emptyOutDir: true,
  },
  server: {
    port: 3100,
    proxy: {
      '/Client/api': 'http://localhost',
      '/integration': 'http://localhost',
    }
  }
})
""";
    }

    private static string GenerateTsConfig()
    {
        return """
{
  "compilerOptions": {
    "target": "ES2020",
    "useDefineForClassFields": true,
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "skipLibCheck": true,
    "moduleResolution": "bundler",
    "allowImportingTsExtensions": true,
    "resolveJsonModule": true,
    "isolatedModules": true,
    "noEmit": true,
    "jsx": "react-jsx",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noFallthroughCasesInSwitch": true
  },
  "include": ["src"],
  "references": [{ "path": "./tsconfig.node.json" }]
}
""";
    }

    private static string GenerateTsConfigNode()
    {
        return """
{
  "compilerOptions": {
    "composite": true,
    "skipLibCheck": true,
    "module": "ESNext",
    "moduleResolution": "bundler",
    "allowSyntheticDefaultImports": true
  },
  "include": ["vite.config.ts"]
}
""";
    }

    private static string GenerateIndexHtml(string appName)
    {
        return $$"""
<!DOCTYPE html>
<html lang="ru">
<head>
  <meta charset="UTF-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1.0" />
  <title>{{appName}} — Directum RX</title>
  <script src="config.js"></script>
</head>
<body>
  <div id="root"></div>
  <script type="module" src="/src/main.tsx"></script>
</body>
</html>
""";
    }

    private static string GenerateConfigJs()
    {
        return """
// Runtime configuration — override API base path per environment.
// In production, set window.PUBLIC_API_PATH to match your IIS routing.
window.PUBLIC_API_PATH = '/Client/api/';
""";
    }

    private static string GenerateMainTsx(string appName)
    {
        _ = appName; // reserved for future use
        return """
import React from 'react'
import ReactDOM from 'react-dom/client'
import { ConfigProvider } from 'antd'
import ruRU from 'antd/locale/ru_RU'
import App from './App'
import './index.css'

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ConfigProvider locale={ruRU} theme={{ token: { colorPrimary: '#1677ff' } }}>
      <App />
    </ConfigProvider>
  </React.StrictMode>
)
""";
    }

    private static string GenerateIndexCss()
    {
        return """
body {
  margin: 0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
}

#root {
  min-height: 100vh;
}
""";
    }

    private static string GenerateViteEnvDts()
    {
        return """
/// <reference types="vite/client" />

interface Window {
  PUBLIC_API_PATH?: string;
}
""";
    }

    #endregion

    #region TypeScript Types

    private static string GenerateTypes(List<EntityInfo> parsedEntities)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"// Auto-generated TypeScript interfaces from MTD: {string.Join(", ", parsedEntities.Select(e => e.Name))}");
        sb.AppendLine();

        foreach (var entity in parsedEntities)
        {
            sb.AppendLine($"export interface {entity.Name} {{");
            sb.AppendLine("  id: number;");
            foreach (var prop in entity.Properties)
            {
                if (string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    continue;

                var fieldName = ToCamelCase(prop.Name);
                var nullable = prop.IsRequired ? "" : "?";
                sb.AppendLine($"  {fieldName}{nullable}: {prop.TsType};");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        // Generate create/update request types
        foreach (var entity in parsedEntities)
        {
            sb.AppendLine($"export interface Create{entity.Name}Request {{");
            foreach (var prop in entity.Properties)
            {
                if (string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    continue;
                var fieldName = ToCamelCase(prop.Name);
                var nullable = prop.IsRequired ? "" : "?";
                sb.AppendLine($"  {fieldName}{nullable}: {prop.TsType};");
            }
            sb.AppendLine("}");
            sb.AppendLine();

            sb.AppendLine($"export interface Update{entity.Name}Request {{");
            sb.AppendLine("  id: number;");
            foreach (var prop in entity.Properties)
            {
                if (string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    continue;
                var fieldName = ToCamelCase(prop.Name);
                sb.AppendLine($"  {fieldName}?: {prop.TsType};");
            }
            sb.AppendLine("}");
            sb.AppendLine();
        }

        sb.AppendLine("export interface ApiResponse {");
        sb.AppendLine("  success: boolean;");
        sb.AppendLine("  id?: number;");
        sb.AppendLine("  error?: string;");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region API Service

    private static string GenerateApiService(List<EntityInfo> parsedEntities, string appNameLower)
    {
        var sb = new StringBuilder();
        sb.AppendLine("import type {");
        foreach (var entity in parsedEntities)
        {
            sb.AppendLine($"  {entity.Name},");
            sb.AppendLine($"  Create{entity.Name}Request,");
            sb.AppendLine($"  Update{entity.Name}Request,");
        }
        sb.AppendLine("  ApiResponse,");
        sb.AppendLine("} from '../types';");
        sb.AppendLine();
        sb.AppendLine("const getApiBase = () => window.PUBLIC_API_PATH || '/Client/api/';");
        sb.AppendLine();
        sb.AppendLine("async function request<T>(url: string, options?: RequestInit): Promise<T> {");
        sb.AppendLine("  const response = await fetch(url, {");
        sb.AppendLine("    ...options,");
        sb.AppendLine("    credentials: 'include',");
        sb.AppendLine("    headers: { 'Content-Type': 'application/json', ...options?.headers },");
        sb.AppendLine("  });");
        sb.AppendLine("  if (!response.ok) throw new Error(`API Error: ${response.status}`);");
        sb.AppendLine("  return response.json();");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine($"export const {appNameLower}Api = {{");

        foreach (var entity in parsedEntities)
        {
            var nameLower = ToCamelCase(entity.Name);
            var pathSegment = entity.Name.ToLowerInvariant();

            sb.AppendLine($"  // {entity.DisplayName}");
            sb.AppendLine($"  get{entity.Name}s: () =>");
            sb.AppendLine($"    request<{entity.Name}[]>(`${{getApiBase()}}{appNameLower}/{pathSegment}/list`),");
            sb.AppendLine();
            sb.AppendLine($"  get{entity.Name}: (id: number) =>");
            sb.AppendLine($"    request<{entity.Name}>(`${{getApiBase()}}{appNameLower}/{pathSegment}?id=${{id}}`),");
            sb.AppendLine();
            sb.AppendLine($"  create{entity.Name}: (data: Create{entity.Name}Request) =>");
            sb.AppendLine($"    request<ApiResponse>(`${{getApiBase()}}{appNameLower}/{pathSegment}/create`, {{");
            sb.AppendLine("      method: 'POST',");
            sb.AppendLine("      body: JSON.stringify(data),");
            sb.AppendLine("    }),");
            sb.AppendLine();
            sb.AppendLine($"  update{entity.Name}: (data: Update{entity.Name}Request) =>");
            sb.AppendLine($"    request<ApiResponse>(`${{getApiBase()}}{appNameLower}/{pathSegment}/update`, {{");
            sb.AppendLine("      method: 'POST',");
            sb.AppendLine("      body: JSON.stringify(data),");
            sb.AppendLine("    }),");
            sb.AppendLine();
            sb.AppendLine($"  delete{entity.Name}: (id: number) =>");
            sb.AppendLine($"    request<ApiResponse>(`${{getApiBase()}}{appNameLower}/{pathSegment}/delete`, {{");
            sb.AppendLine("      method: 'POST',");
            sb.AppendLine("      body: JSON.stringify({ id }),");
            sb.AppendLine("    }),");
            sb.AppendLine();
        }

        sb.AppendLine("};");

        return sb.ToString();
    }

    #endregion

    #region Layout Components

    private static string GenerateAppLayout()
    {
        return """
import { Layout } from 'antd'
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'

const { Sider, Content } = Layout

export default function AppLayout() {
  return (
    <Layout style={{ minHeight: '100vh' }}>
      <Sider width={220} theme="light" style={{ borderRight: '1px solid #f0f0f0' }}>
        <Sidebar />
      </Sider>
      <Content style={{ padding: 24, background: '#f5f5f5' }}>
        <Outlet />
      </Content>
    </Layout>
  )
}
""";
    }

    private static string GenerateSidebar(List<EntityInfo> parsedEntities, string appName)
    {
        var sb = new StringBuilder();
        sb.AppendLine("import { Menu } from 'antd'");
        sb.AppendLine("import { UnorderedListOutlined, LinkOutlined } from '@ant-design/icons'");
        sb.AppendLine("import { useNavigate, useLocation } from 'react-router-dom'");
        sb.AppendLine();
        sb.AppendLine("export default function Sidebar() {");
        sb.AppendLine("  const navigate = useNavigate()");
        sb.AppendLine("  const location = useLocation()");
        sb.AppendLine();
        sb.AppendLine("  const items = [");

        foreach (var entity in parsedEntities)
        {
            var path = $"/{entity.Name.ToLowerInvariant()}s";
            sb.AppendLine($"    {{ key: '{path}', icon: <UnorderedListOutlined />, label: '{entity.DisplayName}' }},");
        }

        sb.AppendLine("    { type: 'divider' as const },");
        sb.AppendLine($"    {{ key: 'rx', icon: <LinkOutlined />, label: 'Directum RX', onClick: () => window.open('/Client/', '_blank') }},");
        sb.AppendLine("  ]");
        sb.AppendLine();
        sb.AppendLine("  return (");
        sb.AppendLine("    <div style={{ display: 'flex', flexDirection: 'column', height: '100%' }}>");
        sb.AppendLine($"      <div style={{{{ padding: '16px', textAlign: 'center', borderBottom: '1px solid #f0f0f0' }}}}>");
        sb.AppendLine($"        <h2 style={{{{ margin: 0, fontSize: 20, color: '#1677ff' }}}}>{appName}</h2>");
        sb.AppendLine($"        <div style={{{{ fontSize: 12, color: '#999' }}}}>Directum RX</div>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <Menu");
        sb.AppendLine("        mode=\"inline\"");
        sb.AppendLine("        selectedKeys={[");
        sb.AppendLine("          items");
        sb.AppendLine("            .filter((i) => i.key !== 'rx' && !('type' in i))");
        sb.AppendLine("            .map((i) => i.key as string)");
        sb.AppendLine("            .find((key) => location.pathname === key || location.pathname.startsWith(key + '/'))");
        sb.AppendLine("            ?? location.pathname,");
        sb.AppendLine("        ]}");
        sb.AppendLine("        items={items}");
        sb.AppendLine("        onClick={({ key }) => { if (key !== 'rx') navigate(key) }}");
        sb.AppendLine("        style={{ flex: 1, borderRight: 0 }}");
        sb.AppendLine("      />");
        sb.AppendLine("    </div>");
        sb.AppendLine("  )");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region App.tsx

    private static string GenerateAppTsx(List<EntityInfo> parsedEntities)
    {
        var sb = new StringBuilder();
        sb.AppendLine("import { HashRouter, Routes, Route, Navigate } from 'react-router-dom'");
        sb.AppendLine("import AppLayout from './components/layout/AppLayout'");

        foreach (var entity in parsedEntities)
        {
            sb.AppendLine($"import {entity.Name}ListPage from './pages/{entity.Name}ListPage'");
            sb.AppendLine($"import {entity.Name}DetailPage from './pages/{entity.Name}DetailPage'");
            sb.AppendLine($"import {entity.Name}FormPage from './pages/{entity.Name}FormPage'");
        }

        sb.AppendLine();
        sb.AppendLine("export default function App() {");
        sb.AppendLine("  return (");
        sb.AppendLine("    <HashRouter>");
        sb.AppendLine("      <Routes>");
        sb.AppendLine("        <Route element={<AppLayout />}>");

        var firstEntity = parsedEntities[0];
        sb.AppendLine($"          <Route path=\"/\" element={{<Navigate to=\"/{firstEntity.Name.ToLowerInvariant()}s\" replace />}} />");

        foreach (var entity in parsedEntities)
        {
            var path = entity.Name.ToLowerInvariant();
            sb.AppendLine($"          <Route path=\"/{path}s\" element={{<{entity.Name}ListPage />}} />");
            sb.AppendLine($"          <Route path=\"/{path}s/new\" element={{<{entity.Name}FormPage />}} />");
            sb.AppendLine($"          <Route path=\"/{path}s/:id\" element={{<{entity.Name}DetailPage />}} />");
        }

        sb.AppendLine("        </Route>");
        sb.AppendLine("      </Routes>");
        sb.AppendLine("    </HashRouter>");
        sb.AppendLine("  )");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region List Page

    private static string GenerateListPage(EntityInfo entity, string appNameLower)
    {
        var sb = new StringBuilder();
        var nameLower = entity.Name.ToLowerInvariant();

        sb.AppendLine("import { useEffect, useState } from 'react';");
        sb.AppendLine("import { Table, Button, Space, Typography, message } from 'antd';");
        sb.AppendLine("import { PlusOutlined, ReloadOutlined } from '@ant-design/icons';");
        sb.AppendLine("import { useNavigate } from 'react-router-dom';");
        sb.AppendLine($"import type {{ {entity.Name} }} from '../types';");
        sb.AppendLine($"import {{ {appNameLower}Api }} from '../services/api';");
        sb.AppendLine();
        sb.AppendLine($"const {{ Title }} = Typography;");
        sb.AppendLine();
        sb.AppendLine($"export default function {entity.Name}ListPage() {{");
        sb.AppendLine("  const navigate = useNavigate();");
        sb.AppendLine($"  const [data, setData] = useState<{entity.Name}[]>([]);");
        sb.AppendLine("  const [loading, setLoading] = useState(false);");
        sb.AppendLine();
        sb.AppendLine("  const loadData = async () => {");
        sb.AppendLine("    setLoading(true);");
        sb.AppendLine("    try {");
        sb.AppendLine($"      const items = await {appNameLower}Api.get{entity.Name}s();");
        sb.AppendLine("      setData(items);");
        sb.AppendLine("    } catch (e: unknown) {");
        sb.AppendLine("      message.error(e instanceof Error ? e.message : 'Ошибка загрузки');");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      setLoading(false);");
        sb.AppendLine("    }");
        sb.AppendLine("  };");
        sb.AppendLine();
        sb.AppendLine("  useEffect(() => { loadData(); }, []);");
        sb.AppendLine();
        sb.AppendLine("  const columns = [");

        // Generate columns for visible properties (limit to first 6 for table)
        var visibleProps = entity.Properties.Take(6).ToList();
        foreach (var prop in visibleProps)
        {
            var fieldName = ToCamelCase(prop.Name);
            sb.AppendLine($"    {{ title: '{prop.Label}', dataIndex: '{fieldName}', key: '{fieldName}' }},");
        }

        sb.AppendLine("    {");
        sb.AppendLine("      title: '',");
        sb.AppendLine("      key: 'actions',");
        sb.AppendLine($"      render: (_: unknown, record: {entity.Name}) => (");
        sb.AppendLine("        <Button type=\"link\" onClick={() => navigate(`/{nameLower}s/${record.id}`)}>Открыть</Button>");
        sb.AppendLine("      ),");
        sb.AppendLine("    },");
        sb.AppendLine("  ];");
        sb.AppendLine();
        sb.AppendLine("  return (");
        sb.AppendLine("    <div>");
        sb.AppendLine("      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>");
        sb.AppendLine($"        <Title level={{4}} style={{{{ margin: 0 }}}}>{entity.DisplayName}</Title>");
        sb.AppendLine("        <Space>");
        sb.AppendLine("          <Button icon={<ReloadOutlined />} onClick={loadData}>Обновить</Button>");
        sb.AppendLine($"          <Button type=\"primary\" icon={{<PlusOutlined />}} onClick={{() => navigate('/{nameLower}s/new')}}>Создать</Button>");
        sb.AppendLine("        </Space>");
        sb.AppendLine("      </div>");
        sb.AppendLine("      <Table");
        sb.AppendLine("        columns={columns}");
        sb.AppendLine("        dataSource={data}");
        sb.AppendLine("        rowKey=\"id\"");
        sb.AppendLine("        loading={loading}");
        sb.AppendLine("        pagination={{ pageSize: 20, showSizeChanger: true }}");
        sb.AppendLine("      />");
        sb.AppendLine("    </div>");
        sb.AppendLine("  );");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Detail Page

    private static string GenerateDetailPage(EntityInfo entity, string appNameLower)
    {
        var sb = new StringBuilder();
        var nameLower = entity.Name.ToLowerInvariant();

        sb.AppendLine("import { useEffect, useState } from 'react';");
        sb.AppendLine("import { useParams, useNavigate } from 'react-router-dom';");
        sb.AppendLine("import { Spin, Alert, Descriptions, Button, Space, Breadcrumb, Card, Typography, message } from 'antd';");
        sb.AppendLine("import { ArrowLeftOutlined, EditOutlined, DeleteOutlined, ExportOutlined } from '@ant-design/icons';");
        sb.AppendLine($"import type {{ {entity.Name} }} from '../types';");
        sb.AppendLine($"import {{ {appNameLower}Api }} from '../services/api';");
        sb.AppendLine();
        sb.AppendLine("const { Title } = Typography;");
        sb.AppendLine();

        // Entity GUID for opening in Directum RX
        sb.AppendLine($"const ENTITY_GUID = '{entity.Guid}';");
        sb.AppendLine();

        sb.AppendLine($"export default function {entity.Name}DetailPage() {{");
        sb.AppendLine("  const { id } = useParams<{ id: string }>();");
        sb.AppendLine("  const navigate = useNavigate();");
        sb.AppendLine($"  const [item, setItem] = useState<{entity.Name} | null>(null);");
        sb.AppendLine("  const [loading, setLoading] = useState(true);");
        sb.AppendLine("  const [error, setError] = useState<string | null>(null);");
        sb.AppendLine();
        sb.AppendLine("  useEffect(() => {");
        sb.AppendLine("    if (!id) return;");
        sb.AppendLine("    setLoading(true);");
        sb.AppendLine($"    {appNameLower}Api.get{entity.Name}(Number(id))");
        sb.AppendLine("      .then(setItem)");
        sb.AppendLine("      .catch((e: Error) => setError(e.message))");
        sb.AppendLine("      .finally(() => setLoading(false));");
        sb.AppendLine("  }, [id]);");
        sb.AppendLine();
        sb.AppendLine("  const handleDelete = async () => {");
        sb.AppendLine("    if (!id) return;");
        sb.AppendLine("    try {");
        sb.AppendLine($"      const result = await {appNameLower}Api.delete{entity.Name}(Number(id));");
        sb.AppendLine("      if (result.success) {");
        sb.AppendLine("        message.success('Удалено');");
        sb.AppendLine($"        navigate('/{nameLower}s');");
        sb.AppendLine("      } else {");
        sb.AppendLine("        message.error(result.error || 'Ошибка удаления');");
        sb.AppendLine("      }");
        sb.AppendLine("    } catch (e: unknown) {");
        sb.AppendLine("      message.error(e instanceof Error ? e.message : 'Ошибка');");
        sb.AppendLine("    }");
        sb.AppendLine("  };");
        sb.AppendLine();
        sb.AppendLine("  if (loading) return <div style={{ display: 'flex', justifyContent: 'center', padding: 64 }}><Spin size=\"large\" /></div>;");
        sb.AppendLine("  if (error) return <Alert type=\"error\" message=\"Ошибка загрузки\" description={error} showIcon />;");
        sb.AppendLine("  if (!item) return <Alert type=\"warning\" message=\"Запись не найдена\" showIcon />;");
        sb.AppendLine();
        sb.AppendLine("  return (");
        sb.AppendLine("    <div style={{ maxWidth: 900, margin: '0 auto' }}>");
        sb.AppendLine("      <Breadcrumb");
        sb.AppendLine("        style={{ marginBottom: 16 }}");
        sb.AppendLine("        items={[");
        sb.AppendLine($"          {{ title: <a onClick={{() => navigate('/{nameLower}s')}}>{entity.DisplayName}</a> }},");

        // Use Name property or first string property for breadcrumb
        var nameField = entity.Properties.FirstOrDefault(p =>
            string.Equals(p.Name, "Name", StringComparison.OrdinalIgnoreCase));
        var breadcrumbField = nameField != null ? ToCamelCase(nameField.Name) : "id";
        sb.AppendLine($"          {{ title: item.{breadcrumbField} }},");

        sb.AppendLine("        ]}");
        sb.AppendLine("      />");
        sb.AppendLine();
        sb.AppendLine("      <Card>");
        sb.AppendLine("        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24 }}>");
        sb.AppendLine($"          <Title level={{4}} style={{{{ margin: 0 }}}}>{entity.DisplayName}</Title>");
        sb.AppendLine("          <Space>");
        sb.AppendLine($"            <Button icon={{<EditOutlined />}} onClick={{() => navigate(`/{nameLower}s/${{id}}`)}}>Редактировать</Button>");
        sb.AppendLine("            <Button danger icon={<DeleteOutlined />} onClick={handleDelete}>Удалить</Button>");
        sb.AppendLine($"            <Button icon={{<ArrowLeftOutlined />}} onClick={{() => navigate('/{nameLower}s')}}>Назад</Button>");
        sb.AppendLine("            <Button type=\"primary\" icon={<ExportOutlined />} onClick={() => window.open(`/Client/#/card/${ENTITY_GUID}/${id}`, '_blank')}>Открыть в Directum RX</Button>");
        sb.AppendLine("          </Space>");
        sb.AppendLine("        </div>");
        sb.AppendLine();
        sb.AppendLine("        <Descriptions bordered column={2} size=\"middle\">");

        foreach (var prop in entity.Properties)
        {
            var fieldName = ToCamelCase(prop.Name);
            sb.AppendLine($"          <Descriptions.Item label=\"{prop.Label}\">{{item.{fieldName} ?? '—'}}</Descriptions.Item>");
        }

        sb.AppendLine("        </Descriptions>");
        sb.AppendLine("      </Card>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  );");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Form Page

    private static string GenerateFormPage(EntityInfo entity, string appNameLower)
    {
        var sb = new StringBuilder();
        var nameLower = entity.Name.ToLowerInvariant();

        sb.AppendLine("import { useState } from 'react';");
        sb.AppendLine("import { Form, Input, InputNumber, DatePicker, Select, Button, Card, Breadcrumb, Space, Typography, message, Switch } from 'antd';");
        sb.AppendLine("import { ArrowLeftOutlined, SaveOutlined } from '@ant-design/icons';");
        sb.AppendLine("import { useNavigate } from 'react-router-dom';");
        sb.AppendLine($"import {{ {appNameLower}Api }} from '../services/api';");
        sb.AppendLine();
        sb.AppendLine("const { Title } = Typography;");
        sb.AppendLine("const { TextArea } = Input;");
        sb.AppendLine();
        sb.AppendLine($"export default function {entity.Name}FormPage() {{");
        sb.AppendLine("  const navigate = useNavigate();");
        sb.AppendLine("  const [form] = Form.useForm();");
        sb.AppendLine("  const [saving, setSaving] = useState(false);");
        sb.AppendLine();
        sb.AppendLine("  const onFinish = async (values: Record<string, unknown>) => {");
        sb.AppendLine("    setSaving(true);");
        sb.AppendLine("    try {");
        sb.AppendLine($"      const result = await {appNameLower}Api.create{entity.Name}(values as any);");
        sb.AppendLine("      if (result.success) {");
        sb.AppendLine("        message.success('Создано');");
        sb.AppendLine($"        navigate('/{nameLower}s');");
        sb.AppendLine("      } else {");
        sb.AppendLine("        message.error(result.error || 'Ошибка создания');");
        sb.AppendLine("      }");
        sb.AppendLine("    } catch (e: unknown) {");
        sb.AppendLine("      message.error(e instanceof Error ? e.message : 'Неизвестная ошибка');");
        sb.AppendLine("    } finally {");
        sb.AppendLine("      setSaving(false);");
        sb.AppendLine("    }");
        sb.AppendLine("  };");
        sb.AppendLine();
        sb.AppendLine("  return (");
        sb.AppendLine("    <div style={{ maxWidth: 800, margin: '0 auto' }}>");
        sb.AppendLine("      <Breadcrumb");
        sb.AppendLine("        style={{ marginBottom: 16 }}");
        sb.AppendLine("        items={[");
        sb.AppendLine($"          {{ title: <a onClick={{() => navigate('/{nameLower}s')}}>{entity.DisplayName}</a> }},");
        sb.AppendLine($"          {{ title: 'Новая запись' }},");
        sb.AppendLine("        ]}");
        sb.AppendLine("      />");
        sb.AppendLine();
        sb.AppendLine("      <Card>");
        sb.AppendLine($"        <Title level={{4}} style={{{{ marginBottom: 24 }}}}>Новая запись: {entity.DisplayName}</Title>");
        sb.AppendLine();
        sb.AppendLine("        <Form form={form} layout=\"vertical\" onFinish={onFinish} requiredMark=\"optional\">");

        foreach (var prop in entity.Properties)
        {
            if (string.Equals(prop.Name, "Id", StringComparison.OrdinalIgnoreCase))
                continue;

            var fieldName = ToCamelCase(prop.Name);
            var requiredRule = prop.IsRequired
                ? $" rules={{[{{ required: true, message: 'Заполните поле' }}]}}"
                : "";

            sb.AppendLine($"          <Form.Item name=\"{fieldName}\" label=\"{prop.Label}\"{requiredRule}>");

            switch (prop.FormComponent)
            {
                case "InputNumber":
                    sb.AppendLine("            <InputNumber style={{ width: '100%' }} />");
                    break;
                case "DatePicker":
                    sb.AppendLine("            <DatePicker style={{ width: '100%' }} format=\"DD.MM.YYYY\" />");
                    break;
                case "Select":
                    sb.AppendLine("            <Select showSearch allowClear placeholder=\"Выберите...\" filterOption={(input, option) => ((option?.label as string) ?? '').toLowerCase().includes(input.toLowerCase())} options={[]} />");
                    break;
                case "Switch":
                    sb.AppendLine("            <Switch />");
                    break;
                case "TextArea":
                    sb.AppendLine("            <TextArea rows={4} />");
                    break;
                default:
                    sb.AppendLine("            <Input />");
                    break;
            }

            sb.AppendLine("          </Form.Item>");
        }

        sb.AppendLine();
        sb.AppendLine("          <Form.Item style={{ marginBottom: 0 }}>");
        sb.AppendLine("            <Space>");
        sb.AppendLine("              <Button type=\"primary\" htmlType=\"submit\" icon={<SaveOutlined />} loading={saving}>Сохранить</Button>");
        sb.AppendLine($"              <Button icon={{<ArrowLeftOutlined />}} onClick={{() => navigate('/{nameLower}s')}}>Отмена</Button>");
        sb.AppendLine("            </Space>");
        sb.AppendLine("          </Form.Item>");
        sb.AppendLine("        </Form>");
        sb.AppendLine("      </Card>");
        sb.AppendLine("    </div>");
        sb.AppendLine("  );");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #endregion

    #region Helpers

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;
        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    private record GeneratedFile(string Title, string Language, string Content);

    private sealed class PropInfo(
        string name, string code, string shortType, string tsType,
        string formComponent, bool isRequired, string? navigationEntityGuid)
    {
        public string Name { get; } = name;
        public string Code { get; } = code;
        public string ShortType { get; } = shortType;
        public string TsType { get; } = tsType;
        public string FormComponent { get; } = formComponent;
        public bool IsRequired { get; } = isRequired;
        public string? NavigationEntityGuid { get; } = navigationEntityGuid;
        public string Label { get; set; } = name;
    }

    private sealed record EntityInfo(
        string Name,
        string Guid,
        string DisplayName,
        List<PropInfo> Properties);

    #endregion
}
