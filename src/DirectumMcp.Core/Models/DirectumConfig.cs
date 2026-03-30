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

        // Fallback defaults for local development
        if (string.IsNullOrEmpty(url))
            url = "http://localhost/Integration/odata";
        if (string.IsNullOrEmpty(user))
            user = "Administrator";
        if (string.IsNullOrEmpty(pass))
            pass = string.Empty;

        // Validation warnings
        if (string.IsNullOrEmpty(pass))
            Console.Error.WriteLine("WARNING: RX_PASSWORD is empty — OData requests will likely fail with 401.");
        if (!url.Contains("://"))
            Console.Error.WriteLine($"WARNING: RX_ODATA_URL '{url}' looks invalid (no scheme). Expected: http(s)://host/Integration/odata");

        return new DirectumConfig
        {
            ODataUrl = url,
            Username = user,
            Password = pass
        };
    }
}
