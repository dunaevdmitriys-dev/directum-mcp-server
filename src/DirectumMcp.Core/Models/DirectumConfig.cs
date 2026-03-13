namespace DirectumMcp.Core.Models;

/// <summary>
/// Configuration for connecting to Directum RX OData Integration Service.
/// </summary>
public sealed record DirectumConfig
{
    /// <summary>
    /// Base URL of the OData service (e.g., http://localhost/Integration/odata).
    /// </summary>
    public required string ODataUrl { get; init; }

    /// <summary>
    /// Username for Basic Auth.
    /// </summary>
    public required string Username { get; init; }

    /// <summary>
    /// Password for Basic Auth.
    /// </summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>
    /// Creates config from environment variables.
    /// </summary>
    public static DirectumConfig FromEnvironment()
    {
        return new DirectumConfig
        {
            ODataUrl = Environment.GetEnvironmentVariable("RX_ODATA_URL")
                       ?? throw new InvalidOperationException("RX_ODATA_URL environment variable is not set."),
            Username = Environment.GetEnvironmentVariable("RX_USERNAME")
                       ?? throw new InvalidOperationException("RX_USERNAME environment variable is not set."),
            Password = Environment.GetEnvironmentVariable("RX_PASSWORD") ?? string.Empty
        };
    }
}
