using System.ComponentModel;
using System.IO.Compression;
using System.Xml.Linq;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class DeployToStandTool
{
    [McpServerTool(Name = "deploy_to_stand")]
    [Description("Оркестрация деплоя .dat пакета на стенд Directum RX. " +
                 "ОПАСНЫЙ инструмент — по умолчанию работает в dry-run режиме (только показывает план). " +
                 "Для реального деплоя укажите confirm=true и dry_run=false. " +
                 "DeploymentTool.exe НИКОГДА не запускается автоматически — только показывается команда.")]
    public async Task<string> Deploy(
        [Description("Путь к .dat файлу пакета Directum RX")]
        string dat_path,
        [Description("Подтверждение деплоя. Если false (по умолчанию) — только dry-run план без каких-либо действий")]
        bool confirm = false,
        [Description("Режим dry-run: только показать план без выполнения (по умолчанию true). " +
                     "Чтобы скопировать пакет в staging, укажите dry_run=false и confirm=true")]
        bool dry_run = true)
    {
        // PathGuard check
        if (!PathGuard.IsAllowed(dat_path))
            return PathGuard.DenyMessage(dat_path);

        // Step 1: Validate .dat file exists
        if (!File.Exists(dat_path))
            return $"**ОШИБКА**: Файл не найден: `{dat_path}`";

        // Step 2: Validate it's a ZIP with PackageInfo.xml
        var (packageName, packageVersion, validationError, hasPackageInfo) = await ValidatePackage(dat_path);
        if (validationError != null)
            return validationError;

        // Collect info for report
        var fileInfo = new FileInfo(dat_path);
        long fileSizeKb = fileInfo.Length / 1024;
        string packageInfoLine = hasPackageInfo
            ? $"Name={packageName}, Version={packageVersion}"
            : "(PackageInfo.xml отсутствует — предупреждение)";

        // Get env vars
        string? deploymentToolPath = Environment.GetEnvironmentVariable("DEPLOYMENT_TOOL_PATH");
        string? stagingPath = Environment.GetEnvironmentVariable("DEPLOYMENT_STAGING_PATH");

        string stagingDatPath = string.IsNullOrEmpty(stagingPath)
            ? Path.Combine("<DEPLOYMENT_STAGING_PATH>", Path.GetFileName(dat_path))
            : Path.Combine(stagingPath, Path.GetFileName(dat_path));

        string deploymentToolExe = string.IsNullOrEmpty(deploymentToolPath)
            ? "<DEPLOYMENT_TOOL_PATH>\\DeploymentTool.exe"
            : deploymentToolPath;

        bool isDryRun = dry_run || !confirm;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("# План деплоя");
        sb.AppendLine();
        sb.AppendLine($"**Пакет:** `{dat_path}`");
        sb.AppendLine($"**Размер:** {fileSizeKb} KB");
        sb.AppendLine($"**PackageInfo:** {packageInfoLine}");
        sb.AppendLine();

        if (isDryRun)
        {
            sb.AppendLine("> **Режим:** DRY-RUN (только план, без выполнения)");
            sb.AppendLine("> Чтобы выполнить деплой, укажите `confirm=true` и `dry_run=false`.");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("> **Режим:** РЕАЛЬНЫЙ ДЕПЛОЙ (confirm=true, dry_run=false)");
            sb.AppendLine();
        }

        sb.AppendLine("## Шаги");
        sb.AppendLine();

        // Step 1 — validation (already done)
        sb.AppendLine("1. ✅ Валидация пакета — OK");
        if (!hasPackageInfo)
            sb.AppendLine("   > ⚠️ **Предупреждение**: PackageInfo.xml отсутствует в архиве");

        // Step 2 — stop services
        string stopServicesStatus = isDryRun ? "⏳" : "⏳";
        sb.AppendLine($"2. {stopServicesStatus} Остановка сервисов");
        sb.AppendLine("   ```powershell");
        sb.AppendLine("   Stop-WebAppPool -Name \"DirectumRX\"");
        sb.AppendLine("   Stop-Service DirectumRXServiceRunner");
        sb.AppendLine("   ```");

        // Step 3 — copy to staging
        string copyStatus = "⏳";
        string copyNote = "";

        if (!isDryRun)
        {
            if (string.IsNullOrEmpty(stagingPath))
            {
                copyStatus = "❌";
                copyNote = "   > ❌ **Ошибка**: переменная окружения `DEPLOYMENT_STAGING_PATH` не задана";
            }
            else
            {
                try
                {
                    Directory.CreateDirectory(stagingPath);
                    File.Copy(dat_path, stagingDatPath, overwrite: true);
                    copyStatus = "✅";
                    copyNote = $"   > ✅ Файл скопирован в `{stagingDatPath}`";
                }
                catch (Exception ex)
                {
                    copyStatus = "❌";
                    copyNote = $"   > ❌ **Ошибка копирования**: {ex.Message}";
                }
            }
        }

        sb.AppendLine($"3. {copyStatus} Копирование в staging");
        sb.AppendLine("   ```powershell");
        sb.AppendLine($"   Copy-Item \"{dat_path}\" \"{stagingDatPath}\"");
        sb.AppendLine("   ```");
        if (!string.IsNullOrEmpty(copyNote))
            sb.AppendLine(copyNote);

        // Step 4 — publish via DeploymentTool
        string publishStatus = "⏳";
        string toolCheckNote = "";

        if (!isDryRun)
        {
            if (string.IsNullOrEmpty(deploymentToolPath))
            {
                publishStatus = "⚠️";
                toolCheckNote = "   > ⚠️ **Предупреждение**: переменная окружения `DEPLOYMENT_TOOL_PATH` не задана. Запустите команду вручную.";
            }
            else if (!File.Exists(deploymentToolPath))
            {
                publishStatus = "⚠️";
                toolCheckNote = $"   > ⚠️ **Предупреждение**: DeploymentTool.exe не найден по пути `{deploymentToolPath}`. Запустите команду вручную.";
            }
            else
            {
                publishStatus = "⏳";
                toolCheckNote = "   > ✅ DeploymentTool.exe найден. Запустите команду ниже вручную.";
            }
        }

        sb.AppendLine($"4. {publishStatus} Публикация");
        sb.AppendLine("   ```powershell");
        sb.AppendLine($"   & \"{deploymentToolExe}\" --package=\"{stagingDatPath}\" --force");
        sb.AppendLine("   ```");
        if (!string.IsNullOrEmpty(toolCheckNote))
            sb.AppendLine(toolCheckNote);
        sb.AppendLine("   > ⚠️ **ВАЖНО**: DeploymentTool.exe запускается только вручную. Автоматический запуск заблокирован из соображений безопасности.");

        // Step 5 — restart services
        sb.AppendLine("5. ⏳ Перезапуск сервисов");
        sb.AppendLine("   ```powershell");
        sb.AppendLine("   Start-WebAppPool -Name \"DirectumRX\"");
        sb.AppendLine("   Start-Service DirectumRXServiceRunner");
        sb.AppendLine("   ```");

        sb.AppendLine();

        if (isDryRun)
        {
            sb.AppendLine("---");
            sb.AppendLine("**Итог**: Dry-run завершён. Никаких изменений не произведено.");
            sb.AppendLine("Для реального деплоя запустите с `confirm=true` и `dry_run=false`, затем выполните шаги 2–5 вручную.");
        }
        else
        {
            sb.AppendLine("---");
            sb.AppendLine("**Итог**: Подготовка завершена. Выполните шаги 2, 4 и 5 вручную согласно плану выше.");
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Validates the .dat file: must be a valid ZIP containing PackageInfo.xml.
    /// Returns (name, version, errorMessage, hasPackageInfo).
    /// errorMessage is non-null if the file is completely invalid.
    /// hasPackageInfo=false is a warning, not an error.
    /// </summary>
    private static async Task<(string Name, string Version, string? Error, bool HasPackageInfo)> ValidatePackage(string datPath)
    {
        try
        {
            using var stream = File.OpenRead(datPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

            var packageInfoEntry = archive.GetEntry("PackageInfo.xml");
            if (packageInfoEntry == null)
            {
                // Warning only — not a hard error per spec
                return ("(неизвестно)", "(неизвестно)", null, false);
            }

            using var entryStream = packageInfoEntry.Open();
            using var reader = new StreamReader(entryStream, System.Text.Encoding.UTF8);
            var xml = await reader.ReadToEndAsync();

            string name = "(неизвестно)";
            string version = "(неизвестно)";
            try
            {
                var doc = XDocument.Parse(xml);
                name = doc.Root?.Element("Name")?.Value ?? "(неизвестно)";
                version = doc.Root?.Element("Version")?.Value ?? "(неизвестно)";
            }
            catch
            {
                // XML parse error — keep defaults
            }

            return (name, version, null, true);
        }
        catch (InvalidDataException)
        {
            return ("", "", $"**ОШИБКА**: Файл `{datPath}` не является валидным ZIP-архивом.", false);
        }
        catch (Exception ex)
        {
            return ("", "", $"**ОШИБКА**: Не удалось открыть файл `{datPath}`: {ex.Message}", false);
        }
    }
}
