using System.Reflection;
using DirectumMcp.Core.Services;

namespace DirectumMcp.Core.Pipeline;

/// <summary>
/// Registry of all tools available for pipeline execution.
/// Auto-discovers IPipelineStep implementations via reflection.
/// </summary>
public class PipelineToolRegistry
{
    private readonly Dictionary<string, IPipelineStep> _steps = new(StringComparer.OrdinalIgnoreCase);

    public PipelineToolRegistry()
    {
        var assembly = typeof(IPipelineStep).Assembly;
        var stepTypes = assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPipelineStep).IsAssignableFrom(t));

        foreach (var type in stepTypes)
        {
            if (Activator.CreateInstance(type) is IPipelineStep step)
                Register(step);
        }
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
