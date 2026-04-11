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
        var url = Environment.GetEnvironmentVariable("RX_ODATA_URL");
        var user = Environment.GetEnvironmentVariable("RX_USERNAME");
        var pass = Environment.GetEnvironmentVariable("RX_PASSWORD");

        if (string.IsNullOrEmpty(url))
            throw new InvalidOperationException(
                "RX_ODATA_URL environment variable is required. " +
                "Example: http://your-stand/Integration/odata. " +
                "See .env.example for all required variables.");

        if (string.IsNullOrEmpty(user))
            throw new InvalidOperationException(
                "RX_USERNAME environment variable is required. " +
                "Example: Administrator. " +
                "See .env.example for all required variables.");

        if (string.IsNullOrEmpty(pass))
            Console.Error.WriteLine("WARNING: RX_PASSWORD is empty — OData requests will likely fail with 401.");

        if (!url.Contains("://"))
            Console.Error.WriteLine($"WARNING: RX_ODATA_URL '{url}' looks invalid (no scheme). Expected: http(s)://host/Integration/odata");

        return new DirectumConfig
        {
            ODataUrl = url,
            Username = user,
            Password = pass ?? string.Empty
        };
    }
}
