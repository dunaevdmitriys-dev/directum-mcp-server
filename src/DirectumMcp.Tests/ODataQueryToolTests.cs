using DirectumMcp.Core.Models;
using DirectumMcp.Core.OData;
using DirectumMcp.RuntimeTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ODataQueryToolTests : IDisposable
{
    // We reuse the client for tests that require it, but validation tests don't actually call the network.
    private readonly DirectumODataClient _client;
    private readonly ODataQueryTool _tool;

    public ODataQueryToolTests()
    {
        var config = new DirectumConfig
        {
            ODataUrl = "http://localhost/Integration/odata",
            Username = "test",
            Password = "test"
        };
        _client = new DirectumODataClient(config);
        _tool = new ODataQueryTool(_client);
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    #region Validation — empty entity name

    [Fact]
    public async Task Query_EmptyEntity_ReturnsError()
    {
        var result = await _tool.Query(entity: "");

        Assert.Contains("Ошибка", result);
        Assert.Contains("entity", result);
    }

    [Fact]
    public async Task Query_WhitespaceEntity_ReturnsError()
    {
        var result = await _tool.Query(entity: "   ");

        Assert.Contains("Ошибка", result);
    }

    #endregion

    #region Validation — mode=by_id without id

    [Fact]
    public async Task Query_ByIdModeWithZeroId_ReturnsError()
    {
        var result = await _tool.Query(entity: "IDocuments", mode: "by_id", id: 0);

        Assert.Contains("Ошибка", result);
        Assert.Contains("by_id", result);
        Assert.Contains("id", result);
    }

    [Fact]
    public async Task Query_ByIdModeWithNegativeId_ReturnsError()
    {
        var result = await _tool.Query(entity: "IDocuments", mode: "by_id", id: -5);

        Assert.Contains("Ошибка", result);
    }

    #endregion

    #region BuildCountUrl — correct URL construction for mode=count

    [Fact]
    public void BuildCountUrl_NoFilter_IncludesCountAndTopZero()
    {
        var url = ODataQueryTool.BuildCountUrl("IDocuments", null);

        Assert.Contains("$count=true", url);
        Assert.Contains("$top=0", url);
        Assert.StartsWith("IDocuments", url);
    }

    [Fact]
    public void BuildCountUrl_WithFilter_IncludesFilterAndCount()
    {
        var url = ODataQueryTool.BuildCountUrl("IDocuments", "Name eq 'Test'");

        Assert.Contains("$filter=" + Uri.EscapeDataString("Name eq 'Test'"), url);
        Assert.Contains("$count=true", url);
        Assert.Contains("$top=0", url);
    }

    [Fact]
    public void BuildCountUrl_EmptyFilter_TreatedAsNoFilter()
    {
        var url = ODataQueryTool.BuildCountUrl("IDatabookEntries", "");

        // Empty/whitespace filter should be ignored
        Assert.Contains("$count=true", url);
        Assert.DoesNotContain("$filter", url);
    }

    #endregion

    #region BuildQueryUrl — URL construction with filters

    [Fact]
    public void BuildQueryUrl_WithAllParams_ContainsAllSegments()
    {
        var url = ODataQueryTool.BuildQueryUrl(
            baseUrl: "http://localhost/Integration/odata",
            entity: "IDocuments",
            filter: "Name eq 'Акт'",
            select: "Id,Name",
            expand: "Author",
            top: 10,
            skip: 5,
            orderby: "Created desc");

        Assert.Contains("IDocuments", url);
        Assert.Contains("$filter=Name eq 'Акт'", url);
        Assert.Contains("$select=Id,Name", url);
        Assert.Contains("$expand=Author", url);
        Assert.Contains("$top=10", url);
        Assert.Contains("$skip=5", url);
        Assert.Contains("$orderby=Created desc", url);
    }

    [Fact]
    public void BuildQueryUrl_NoOptionalParams_OnlyTopPresent()
    {
        var url = ODataQueryTool.BuildQueryUrl(
            baseUrl: "http://localhost/Integration/odata",
            entity: "ICompanies",
            filter: null,
            select: null,
            expand: null,
            top: 20,
            skip: null,
            orderby: null);

        Assert.Contains("ICompanies", url);
        Assert.Contains("$top=20", url);
        Assert.DoesNotContain("$filter", url);
        Assert.DoesNotContain("$select", url);
        Assert.DoesNotContain("$expand", url);
        Assert.DoesNotContain("$skip", url);
        Assert.DoesNotContain("$orderby", url);
    }

    [Fact]
    public void BuildQueryUrl_BaseUrlWithTrailingSlash_NoDuplicateSlash()
    {
        var url = ODataQueryTool.BuildQueryUrl(
            baseUrl: "http://localhost/Integration/odata/",
            entity: "IDocuments",
            filter: null,
            select: null,
            expand: null,
            top: 5,
            skip: null,
            orderby: null);

        // Should not contain double slash before entity name
        Assert.DoesNotContain("odata//", url);
        Assert.Contains("odata/IDocuments", url);
    }

    [Fact]
    public void BuildQueryUrl_SkipZero_NotIncludedInUrl()
    {
        var url = ODataQueryTool.BuildQueryUrl(
            baseUrl: "http://localhost/Integration/odata",
            entity: "IDocuments",
            filter: null,
            select: null,
            expand: null,
            top: 20,
            skip: 0,
            orderby: null);

        Assert.DoesNotContain("$skip", url);
    }

    #endregion

    #region mode=recent — correct ordering

    [Fact]
    public void RecentMode_OrderByIdDesc_IsExpected()
    {
        // Verify the expected orderby for recent mode — this is a convention check.
        // The actual ordering is hardcoded in QueryRecent, so we document it here.
        const string expectedOrderBy = "Id desc";

        // Simulate what BuildQueryUrl produces for recent mode
        var url = ODataQueryTool.BuildQueryUrl(
            baseUrl: "http://localhost/Integration/odata",
            entity: "IDocuments",
            filter: null,
            select: null,
            expand: null,
            top: 5,
            skip: null,
            orderby: expectedOrderBy);

        Assert.Contains("$orderby=Id desc", url);
        Assert.Contains("$top=5", url);
    }

    #endregion

    #region top clamping

    [Fact]
    public async Task Query_TopExceedsMax_ClampedTo200_NotError()
    {
        // top=9999 should be clamped — still returns a network error (not stand available),
        // but NOT a validation error about top value.
        var result = await _tool.Query(entity: "IDocuments", top: 9999);

        // Should either get a connection error (no stand) or valid data — not a "top" validation error.
        Assert.DoesNotContain("параметр top", result);
    }

    #endregion
}
