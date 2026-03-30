using System.Text.Json;
using DirectumMcp.Core.Validators;

namespace DirectumMcp.Core.Services;

/// <summary>
/// Core logic for check_package. Used by MCP tool and pipeline.
/// </summary>
public class PackageValidateService : IPipelineStep
{
    public string ToolName => "check_package";

    public async Task<ValidatePackageResult> ValidateAsync(
        string packagePath,
        CancellationToken ct = default)
    {
        var (workspace, error) = await PackageWorkspace.OpenAsync(packagePath, ct: ct);
        if (workspace == null)
            return new ValidatePackageResult
            {
                Success = false,
                Errors = [error!],
                PackagePath = packagePath
            };

        using (workspace)
        {
            var (results, mtdCount, resxCount) = await PackageValidator.RunAllChecksLegacy(workspace);

            int passed = results.Count(r => r.Passed);
            int failed = results.Count(r => !r.Passed);

            return new ValidatePackageResult
            {
                Success = failed == 0,
                PackagePath = packagePath,
                MtdCount = mtdCount,
                ResxCount = resxCount,
                PassedCount = passed,
                FailedCount = failed,
                Checks = results.Select(r => new ValidatePackageResult.CheckInfo(
                    r.Name, r.Passed, r.Issues, r.Fix)).ToList()
            };
        }
    }

    async Task<ServiceResult> IPipelineStep.ExecuteAsync(
        Dictionary<string, JsonElement> parameters, CancellationToken ct)
    {
        var path = parameters.TryGetValue("packagePath", out var el) && el.ValueKind == JsonValueKind.String
            ? el.GetString() ?? ""
            : "";
        return await ValidateAsync(path, ct);
    }
}
