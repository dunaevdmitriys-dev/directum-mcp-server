using DirectumMcp.Core.Services;

namespace DirectumMcp.Core.Pipeline;

/// <summary>
/// Registry of all tools available for pipeline execution.
/// </summary>
public class PipelineToolRegistry
{
    private readonly Dictionary<string, IPipelineStep> _steps = new(StringComparer.OrdinalIgnoreCase);

    public PipelineToolRegistry()
    {
        Register(new ModuleScaffoldService());
        Register(new EntityScaffoldService());
        Register(new FunctionScaffoldService());
        Register(new JobScaffoldService());
        Register(new PackageValidateService());
        Register(new PackageFixService());
        Register(new PackageBuildService());
        Register(new InitializerGenerateService());
        Register(new PreviewCardService());
    }

    public void Register(IPipelineStep step)
    {
        _steps[step.ToolName] = step;
    }

    public IPipelineStep? Get(string toolName)
    {
        return _steps.GetValueOrDefault(toolName);
    }

    public IReadOnlyCollection<string> ToolNames => _steps.Keys;
}
