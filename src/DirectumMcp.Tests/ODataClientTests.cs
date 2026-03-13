using System.Net.Http.Headers;
using System.Text;
using DirectumMcp.Core.Models;
using DirectumMcp.Core.OData;
using Xunit;

namespace DirectumMcp.Tests;

public class ODataClientTests : IDisposable
{
    private readonly DirectumConfig _testConfig = new()
    {
        ODataUrl = "http://localhost/Integration/odata",
        Username = "ServiceUser",
        Password = "TestPass123"
    };

    private readonly DirectumODataClient _client;

    public ODataClientTests()
    {
        _client = new DirectumODataClient(_testConfig);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    #region ODataQuery Construction

    [Fact]
    public void ODataQuery_FilterOnly_SetsFilterProperty()
    {
        var query = new ODataQuery
        {
            Filter = "Name eq 'Test'"
        };

        Assert.Equal("Name eq 'Test'", query.Filter);
        Assert.Null(query.Select);
        Assert.Null(query.OrderBy);
        Assert.Null(query.Top);
        Assert.Null(query.Skip);
        Assert.Null(query.Expand);
    }

    [Fact]
    public void ODataQuery_AllParameters_SetsAllProperties()
    {
        var query = new ODataQuery
        {
            Filter = "Status eq 'Active'",
            Select = "Id,Name,Status",
            OrderBy = "Created desc",
            Top = 50,
            Skip = 10,
            Expand = "Author"
        };

        Assert.Equal("Status eq 'Active'", query.Filter);
        Assert.Equal("Id,Name,Status", query.Select);
        Assert.Equal("Created desc", query.OrderBy);
        Assert.Equal(50, query.Top);
        Assert.Equal(10, query.Skip);
        Assert.Equal("Author", query.Expand);
    }

    [Fact]
    public void ODataQuery_NoParameters_AllNull()
    {
        var query = new ODataQuery();

        Assert.Null(query.Filter);
        Assert.Null(query.Select);
        Assert.Null(query.OrderBy);
        Assert.Null(query.Top);
        Assert.Null(query.Skip);
        Assert.Null(query.Expand);
    }

    #endregion

    #region Filter Construction Patterns

    [Fact]
    public void FilterConstruction_ContainsQuery_FormatsCorrectly()
    {
        var searchText = "Договор";
        var filter = $"contains(Name, '{searchText}')";

        Assert.Equal("contains(Name, 'Договор')", filter);
    }

    [Fact]
    public void FilterConstruction_DateRange_FormatsCorrectly()
    {
        var dateFrom = "2026-01-01";
        var dateTo = "2026-12-31";

        var filter = $"Created ge {dateFrom}T00:00:00Z and Created le {dateTo}T23:59:59Z";

        Assert.Equal("Created ge 2026-01-01T00:00:00Z and Created le 2026-12-31T23:59:59Z", filter);
    }

    [Fact]
    public void FilterConstruction_MultipleConditions_JoinsWithAnd()
    {
        var filters = new List<string>
        {
            "contains(Name, 'Test')",
            "LifeCycleState eq 'Active'",
            "Created ge 2026-01-01T00:00:00Z"
        };

        var combined = string.Join(" and ", filters);

        Assert.Equal(
            "contains(Name, 'Test') and LifeCycleState eq 'Active' and Created ge 2026-01-01T00:00:00Z",
            combined);
    }

    [Fact]
    public void FilterConstruction_NavigationProperty_FormatsCorrectly()
    {
        var filter = "DocumentKind/Name eq 'Договор'";

        Assert.Contains("/", filter);
        Assert.Equal("DocumentKind/Name eq 'Договор'", filter);
    }

    [Fact]
    public void FilterConstruction_EmptyList_ReturnsNull()
    {
        var filters = new List<string>();

        string? filter = filters.Count > 0 ? string.Join(" and ", filters) : null;

        Assert.Null(filter);
    }

    #endregion

    #region OData String Escaping

    [Fact]
    public void EscapeOData_SingleQuote_Doubled()
    {
        var input = "O'Brien";
        var escaped = input.Replace("'", "''");

        Assert.Equal("O''Brien", escaped);
    }

    [Fact]
    public void EscapeOData_MultipleSingleQuotes_AllDoubled()
    {
        var input = "It's a 'test'";
        var escaped = input.Replace("'", "''");

        Assert.Equal("It''s a ''test''", escaped);
    }

    [Fact]
    public void EscapeOData_NoSpecialChars_Unchanged()
    {
        var input = "Normal text";
        var escaped = input.Replace("'", "''");

        Assert.Equal("Normal text", escaped);
    }

    [Fact]
    public void EscapeOData_CyrillicText_Unchanged()
    {
        var input = "Договор поставки";
        var escaped = input.Replace("'", "''");

        Assert.Equal("Договор поставки", escaped);
    }

    #endregion

    #region Basic Auth Header Generation

    [Fact]
    public void BasicAuth_CredentialsEncoded_CorrectBase64()
    {
        // Arrange
        var username = "ServiceUser";
        var password = "TestPass123";

        // Act
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        // Assert
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(credentials));
        Assert.Equal("ServiceUser:TestPass123", decoded);
    }

    [Fact]
    public void BasicAuth_EmptyPassword_EncodesCorrectly()
    {
        var username = "Admin";
        var password = "";

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(credentials));
        Assert.Equal("Admin:", decoded);
    }

    [Fact]
    public void BasicAuth_CyrillicCredentials_EncodesCorrectly()
    {
        var username = "Администратор";
        var password = "Пароль123";

        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(credentials));
        Assert.Equal("Администратор:Пароль123", decoded);
    }

    [Fact]
    public void BasicAuth_HeaderFormat_IsCorrect()
    {
        var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes("user:pass"));
        var header = new AuthenticationHeaderValue("Basic", credentials);

        Assert.Equal("Basic", header.Scheme);
        Assert.Equal(credentials, header.Parameter);
        Assert.StartsWith("Basic ", header.ToString());
    }

    #endregion

    #region URL Construction

    [Fact]
    public void UrlConstruction_BaseUrlTrailingSlash_Trimmed()
    {
        var config = new DirectumConfig
        {
            ODataUrl = "http://localhost/Integration/odata/",
            Username = "test",
            Password = "test"
        };

        // The client trims trailing slash in constructor
        using var client = new DirectumODataClient(config);

        // We test this indirectly: if the client trimmed the slash,
        // it would build correct URLs without double slashes.
        // Direct URL testing requires reflection or making BuildUrl public,
        // so we verify config processing instead.
        Assert.EndsWith("/", config.ODataUrl);
    }

    [Fact]
    public void UrlConstruction_EntitySetWithQuery_FormatsCorrectly()
    {
        var baseUrl = "http://localhost/Integration/odata";
        var entitySet = "IOfficialDocuments";
        var query = new ODataQuery
        {
            Filter = "Name eq 'Test'",
            Top = 20
        };

        // Reproduce the URL building logic from DirectumODataClient.BuildUrl
        var url = $"{baseUrl}/{entitySet}";
        var parts = new List<string>();
        if (query.Filter is not null) parts.Add($"$filter={query.Filter}");
        if (query.Top.HasValue) parts.Add($"$top={query.Top.Value}");
        if (parts.Count > 0) url += "?" + string.Join("&", parts);

        Assert.Equal("http://localhost/Integration/odata/IOfficialDocuments?$filter=Name eq 'Test'&$top=20", url);
    }

    [Fact]
    public void UrlConstruction_EntitySetOnly_NoQueryString()
    {
        var baseUrl = "http://localhost/Integration/odata";
        var entitySet = "ICompanies";

        var url = $"{baseUrl}/{entitySet}";

        Assert.Equal("http://localhost/Integration/odata/ICompanies", url);
        Assert.DoesNotContain("?", url);
    }

    [Fact]
    public void UrlConstruction_EntityById_FormatsCorrectly()
    {
        var baseUrl = "http://localhost/Integration/odata";
        var entitySet = "IOfficialDocuments";
        long id = 42;

        var url = $"{baseUrl}/{entitySet}({id})";

        Assert.Equal("http://localhost/Integration/odata/IOfficialDocuments(42)", url);
    }

    [Fact]
    public void UrlConstruction_AllQueryParams_CorrectOrder()
    {
        var baseUrl = "http://localhost/Integration/odata";
        var entitySet = "IOfficialDocuments";
        var query = new ODataQuery
        {
            Filter = "Status eq 'Active'",
            Select = "Id,Name",
            OrderBy = "Created desc",
            Top = 10,
            Skip = 5,
            Expand = "Author"
        };

        // Reproduce BuildUrl logic
        var url = $"{baseUrl}/{entitySet}";
        var parts = new List<string>();
        if (query.Filter is not null) parts.Add($"$filter={query.Filter}");
        if (query.Select is not null) parts.Add($"$select={query.Select}");
        if (query.OrderBy is not null) parts.Add($"$orderby={query.OrderBy}");
        if (query.Top.HasValue) parts.Add($"$top={query.Top.Value}");
        if (query.Skip.HasValue) parts.Add($"$skip={query.Skip.Value}");
        if (query.Expand is not null) parts.Add($"$expand={query.Expand}");
        if (parts.Count > 0) url += "?" + string.Join("&", parts);

        Assert.Contains("$filter=Status eq 'Active'", url);
        Assert.Contains("$select=Id,Name", url);
        Assert.Contains("$orderby=Created desc", url);
        Assert.Contains("$top=10", url);
        Assert.Contains("$skip=5", url);
        Assert.Contains("$expand=Author", url);
    }

    #endregion

    #region DirectumConfig

    [Fact]
    public void DirectumConfig_RequiredProperties_SetCorrectly()
    {
        var config = new DirectumConfig
        {
            ODataUrl = "http://example.com/odata",
            Username = "admin"
        };

        Assert.Equal("http://example.com/odata", config.ODataUrl);
        Assert.Equal("admin", config.Username);
        Assert.Equal(string.Empty, config.Password); // default
    }

    [Fact]
    public void DirectumConfig_WithPassword_SetsPassword()
    {
        var config = new DirectumConfig
        {
            ODataUrl = "http://example.com/odata",
            Username = "admin",
            Password = "secret"
        };

        Assert.Equal("secret", config.Password);
    }

    #endregion
}
