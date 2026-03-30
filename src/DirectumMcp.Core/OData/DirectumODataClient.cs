using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using DirectumMcp.Core.Helpers;
using DirectumMcp.Core.Models;

namespace DirectumMcp.Core.OData;

/// <summary>
/// HTTP client wrapper for Directum RX OData Integration Service.
/// Supports Basic Auth and standard OData operations.
/// </summary>
public sealed class DirectumODataClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public DirectumODataClient(DirectumConfig config)
    {
        _baseUrl = config.ODataUrl.TrimEnd('/');

        var uri = new Uri(_baseUrl);
        if (uri.Scheme != "https" && !uri.IsLoopback)
        {
            Console.Error.WriteLine($"WARNING: Basic Auth over HTTP to non-localhost host '{uri.Host}' is insecure. Use HTTPS.");
        }

        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>
    /// GET request to an OData entity set with optional query parameters.
    /// </summary>
    public async Task<JsonElement> GetAsync(
        string entitySet,
        string? filter = null,
        string? select = null,
        string? orderby = null,
        int? top = null,
        int? skip = null,
        string? expand = null,
        CancellationToken ct = default)
    {
        var url = BuildUrl(entitySet, filter, select, orderby, top, skip, expand);
        using var response = await _http.GetAsync(url, ct);
        EnsureSuccess(response, url);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return JsonDocument.Parse("{\"value\":[]}").RootElement.Clone();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// GET a single entity by ID.
    /// </summary>
    public async Task<JsonElement> GetByIdAsync(string entitySet, long id, string? select = null, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/{entitySet}({id})";
        if (select is not null)
            url += $"?$select={select}";

        using var response = await _http.GetAsync(url, ct);
        EnsureSuccess(response, url);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return JsonDocument.Parse("{}").RootElement.Clone();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// POST (create) a new entity.
    /// </summary>
    public async Task<JsonElement> PostAsync(string entitySet, object body, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/{entitySet}";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync(url, content, ct);
        EnsureSuccess(response, url);
        var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// POST an OData action on an entity (e.g., Complete, Start, RouteSteps).
    /// </summary>
    public async Task<JsonElement> PostActionAsync(string entitySet, long id, string actionName, object? body = null, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/{entitySet}({id})/{actionName}";
        var json = body is not null ? JsonSerializer.Serialize(body, JsonOptions) : "{}";
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _http.PostAsync(url, content, ct);
        EnsureSuccess(response, url);

        var responseBody = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(responseBody))
            return JsonDocument.Parse("{}").RootElement.Clone();

        using var doc = JsonDocument.Parse(responseBody);
        return doc.RootElement.Clone();
    }

    /// <summary>
    /// PATCH (update) an existing entity.
    /// </summary>
    public async Task<HttpResponseMessage> PatchAsync(string entitySet, long id, object body, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/{entitySet}({id})";
        var json = JsonSerializer.Serialize(body, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Patch, url) { Content = content };
        var response = await _http.SendAsync(request, ct);
        EnsureSuccess(response, url);
        return response;
    }

    /// <summary>
    /// DELETE an entity by ID.
    /// </summary>
    public async Task<HttpResponseMessage> DeleteAsync(string entitySet, long id, CancellationToken ct = default)
    {
        var url = $"{_baseUrl}/{entitySet}({id})";
        var response = await _http.DeleteAsync(url, ct);
        EnsureSuccess(response, url);
        return response;
    }

    /// <summary>
    /// Execute a raw GET request with a custom URL suffix.
    /// Validates suffix to prevent URL injection.
    /// </summary>
    public async Task<JsonElement> GetRawAsync(string urlSuffix, CancellationToken ct = default)
    {
        var (isValid, error) = ODataSanitizer.ValidateUrlSuffix(urlSuffix);
        if (!isValid)
            throw new ArgumentException($"Invalid URL suffix: {error}");

        var url = $"{_baseUrl}/{urlSuffix}";
        using var response = await _http.GetAsync(url, ct);
        EnsureSuccess(response, url);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(body))
            return JsonDocument.Parse("{\"value\":[]}").RootElement.Clone();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private string BuildUrl(string entitySet, string? filter, string? select, string? orderby, int? top, int? skip, string? expand)
    {
        // Validate all parameters before building URL
        var validationError = ODataSanitizer.ValidateAll(entitySet, filter, select, orderby, expand);
        if (validationError is not null)
            throw new ArgumentException($"OData parameter validation failed: {validationError}");

        var url = $"{_baseUrl}/{entitySet}";
        var parts = new List<string>();

        if (filter is not null)
            parts.Add($"$filter={filter}");
        if (select is not null)
            parts.Add($"$select={select}");
        if (orderby is not null)
            parts.Add($"$orderby={orderby}");
        if (top.HasValue)
            parts.Add($"$top={top.Value}");
        if (skip.HasValue)
        {
            parts.Add($"$skip={skip.Value}");
            if (!top.HasValue)
                parts.Add("$top=100");
        }
        if (expand is not null)
            parts.Add($"$expand={expand}");

        if (parts.Count > 0)
            url += "?" + string.Join("&", parts);

        return url;
    }

    private static void EnsureSuccess(HttpResponseMessage response, string url)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized =>
                $"401 Unauthorized — неверные credentials (RX_USERNAME/RX_PASSWORD). URL: {url}",
            HttpStatusCode.Forbidden =>
                $"403 Forbidden — нет прав доступа к ресурсу. URL: {url}",
            HttpStatusCode.NotFound =>
                $"404 Not Found — entity set или ID не существует. URL: {url}",
            HttpStatusCode.ServiceUnavailable =>
                $"503 Service Unavailable — Directum RX Integration Service не запущен. URL: {url}",
            _ => $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}. URL: {url}"
        };

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    public void Dispose()
    {
        _http.Dispose();
    }
}

/// <summary>
/// OData query parameters model for constructing queries externally.
/// </summary>
public sealed record ODataQuery
{
    public string? Filter { get; init; }
    public string? Select { get; init; }
    public string? OrderBy { get; init; }
    public int? Top { get; init; }
    public int? Skip { get; init; }
    public string? Expand { get; init; }
}
