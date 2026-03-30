namespace DirectumMcp.Shared;

/// <summary>
/// SOLUTION_PATH configuration shared by all DevTools servers (Scaffold, Validate, Analyze, Deploy).
/// Registered as singleton in DI.
/// </summary>
public sealed class SolutionPathConfig
{
    public string Path { get; }

    public SolutionPathConfig(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException(
                "SOLUTION_PATH environment variable is required. " +
                "Set it to the root directory of your Directum RX workspace.");

        if (!Directory.Exists(path))
            throw new InvalidOperationException(
                $"SOLUTION_PATH '{path}' does not exist.");

        Path = path;
    }

    public static SolutionPathConfig FromEnvironment() =>
        new(Environment.GetEnvironmentVariable("SOLUTION_PATH") ?? "");
}
