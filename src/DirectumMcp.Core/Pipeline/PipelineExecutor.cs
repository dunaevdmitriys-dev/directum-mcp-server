using System.Text.Json;
using DirectumMcp.Core.Services;

namespace DirectumMcp.Core.Pipeline;

/// <summary>
/// Executes a sequence of pipeline steps, passing context between them.
/// </summary>
public class PipelineExecutor
{
    private readonly PipelineToolRegistry _registry;

    public PipelineExecutor(PipelineToolRegistry? registry = null)
    {
        _registry = registry ?? new PipelineToolRegistry();
    }

    public async Task<PipelineResult> ExecuteAsync(
        PipelineStep[] steps,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var completed = new List<PlaceholderResolver.StepContext>();
        var stepResults = new List<PipelineResult.StepResultInfo>();

        for (int i = 0; i < steps.Length; i++)
        {
            ct.ThrowIfCancellationRequested();
            var step = steps[i];

            // Check condition
            if (!string.IsNullOrWhiteSpace(step.Condition))
            {
                if (!PlaceholderResolver.EvaluateCondition(step.Condition, completed, i))
                {
                    completed.Add(new PlaceholderResolver.StepContext(step.Id, null, "skipped"));
                    stepResults.Add(new PipelineResult.StepResultInfo
                    {
                        Tool = step.Tool,
                        Status = "skipped",
                        Summary = $"Условие `{step.Condition}` не выполнено"
                    });
                    continue;
                }
            }

            // Resolve placeholders
            var resolvedParams = PlaceholderResolver.Resolve(step.Params, completed, i);

            // Find handler
            var handler = _registry.Get(step.Tool);
            if (handler == null)
            {
                stepResults.Add(new PipelineResult.StepResultInfo
                {
                    Tool = step.Tool,
                    Status = "failed",
                    Summary = $"Неизвестный инструмент: {step.Tool}"
                });

                return new PipelineResult
                {
                    Success = false,
                    Steps = stepResults,
                    CompletedCount = i,
                    TotalCount = steps.Length,
                    FailedAtStep = i,
                    Errors = [$"Шаг {i + 1}: неизвестный инструмент '{step.Tool}'. Доступные: {string.Join(", ", _registry.ToolNames)}"]
                };
            }

            // Execute
            progress?.Report($"Шаг {i + 1}/{steps.Length}: {step.Tool}");

            ServiceResult result;
            try
            {
                result = await handler.ExecuteAsync(resolvedParams, ct);
            }
            catch (Exception ex)
            {
                stepResults.Add(new PipelineResult.StepResultInfo
                {
                    Tool = step.Tool,
                    Status = "failed",
                    Summary = $"Исключение: {ex.Message}"
                });

                return new PipelineResult
                {
                    Success = false,
                    Steps = stepResults,
                    CompletedCount = i,
                    TotalCount = steps.Length,
                    FailedAtStep = i,
                    Errors = [$"Шаг {i + 1} ({step.Tool}): {ex.Message}"]
                };
            }

            completed.Add(new PlaceholderResolver.StepContext(step.Id, result, result.Success ? "completed" : "failed"));

            // Build summary from result
            var summary = result.Success
                ? TruncateMarkdown(result.ToMarkdown(), 200)
                : string.Join("; ", result.Errors);

            stepResults.Add(new PipelineResult.StepResultInfo
            {
                Tool = step.Tool,
                Status = result.Success ? "completed" : "failed",
                Summary = summary,
                Result = result
            });

            // Stop on failure
            if (!result.Success)
            {
                return new PipelineResult
                {
                    Success = false,
                    Steps = stepResults,
                    CompletedCount = i,
                    TotalCount = steps.Length,
                    FailedAtStep = i,
                    Errors = result.Errors
                };
            }
        }

        return new PipelineResult
        {
            Success = true,
            Steps = stepResults,
            CompletedCount = steps.Length,
            TotalCount = steps.Length
        };
    }

    private static string TruncateMarkdown(string md, int maxLen)
    {
        // Take first line or truncate
        var firstLine = md.Split('\n').FirstOrDefault(l => !string.IsNullOrWhiteSpace(l))?.Trim() ?? "";
        return firstLine.Length > maxLen ? firstLine[..maxLen] + "..." : firstLine;
    }
}

/// <summary>
/// A single step in a pipeline.
/// </summary>
public record PipelineStep
{
    public string Tool { get; init; } = "";
    public Dictionary<string, JsonElement> Params { get; init; } = new();
    public string? Condition { get; init; }
    public string? Id { get; init; }
}
