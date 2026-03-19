using DirectumMcp.Core.Helpers;
using Xunit;

namespace DirectumMcp.Tests;

public class ODataSanitizerTests
{
    #region EntitySet validation

    [Theory]
    [InlineData("IDocuments", true)]
    [InlineData("ICRMSalesDeals", true)]
    [InlineData("IAssignments", true)]
    [InlineData("My.Entity", true)]
    [InlineData("_Private", true)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("123Starts", false)]
    [InlineData("has space", false)]
    [InlineData("has;semi", false)]
    [InlineData("DROP TABLE", false)]
    public void IsValidEntitySet_Scenarios(string entitySet, bool expected)
    {
        Assert.Equal(expected, ODataSanitizer.IsValidEntitySet(entitySet));
    }

    [Fact]
    public void IsValidEntitySet_TooLong_ReturnsFalse()
    {
        var longName = new string('A', 201);
        Assert.False(ODataSanitizer.IsValidEntitySet(longName));
    }

    #endregion

    #region Filter validation

    [Fact]
    public void ValidateFilter_Null_IsValid()
    {
        var (ok, _) = ODataSanitizer.ValidateFilter(null);
        Assert.True(ok);
    }

    [Fact]
    public void ValidateFilter_NormalFilter_IsValid()
    {
        var (ok, _) = ODataSanitizer.ValidateFilter("Name eq 'Test' and Status eq 'Active'");
        Assert.True(ok);
    }

    [Fact]
    public void ValidateFilter_ContainsFunction_IsValid()
    {
        var (ok, _) = ODataSanitizer.ValidateFilter("contains(Name, 'Договор')");
        Assert.True(ok);
    }

    [Fact]
    public void ValidateFilter_SqlComment_Rejected()
    {
        var (ok, err) = ODataSanitizer.ValidateFilter("Name eq 'test' -- drop table");
        Assert.False(ok);
        Assert.Contains("--", err);
    }

    [Fact]
    public void ValidateFilter_Semicolon_Rejected()
    {
        var (ok, err) = ODataSanitizer.ValidateFilter("Name eq 'test'; DROP TABLE users");
        Assert.False(ok);
        Assert.Contains(";", err);
    }

    [Fact]
    public void ValidateFilter_UnionInjection_Rejected()
    {
        var (ok, err) = ODataSanitizer.ValidateFilter("Id eq 1 UNION SELECT * FROM passwords");
        Assert.False(ok);
        Assert.Contains("UNION", err);
    }

    [Fact]
    public void ValidateFilter_UnbalancedQuotes_Rejected()
    {
        var (ok, err) = ODataSanitizer.ValidateFilter("Name eq 'unbalanced");
        Assert.False(ok);
        Assert.Contains("unbalanced", err);
    }

    [Fact]
    public void ValidateFilter_TooLong_Rejected()
    {
        var longFilter = new string('a', 2001);
        var (ok, err) = ODataSanitizer.ValidateFilter(longFilter);
        Assert.False(ok);
        Assert.Contains("too long", err);
    }

    #endregion

    #region Select/Expand validation

    [Theory]
    [InlineData(null, true)]
    [InlineData("Id,Name,Status", true)]
    [InlineData("Author/Name", true)]
    [InlineData("Id; DROP TABLE", false)]
    public void ValidateSelectExpand_Scenarios(string? value, bool expectedValid)
    {
        var (ok, _) = ODataSanitizer.ValidateSelectExpand(value, "$select");
        Assert.Equal(expectedValid, ok);
    }

    #endregion

    #region OrderBy validation

    [Theory]
    [InlineData(null, true)]
    [InlineData("Created desc", true)]
    [InlineData("Name asc, Id desc", true)]
    [InlineData("Name; DROP", false)]
    public void ValidateOrderBy_Scenarios(string? value, bool expectedValid)
    {
        var (ok, _) = ODataSanitizer.ValidateOrderBy(value);
        Assert.Equal(expectedValid, ok);
    }

    #endregion

    #region URL suffix validation

    [Fact]
    public void ValidateUrlSuffix_Normal_IsValid()
    {
        var (ok, _) = ODataSanitizer.ValidateUrlSuffix("IDocuments?$top=10&$filter=Name eq 'Test'");
        Assert.True(ok);
    }

    [Fact]
    public void ValidateUrlSuffix_PathTraversal_Rejected()
    {
        var (ok, err) = ODataSanitizer.ValidateUrlSuffix("../../etc/passwd");
        Assert.False(ok);
        Assert.Contains("..", err);
    }

    [Fact]
    public void ValidateUrlSuffix_Backslash_Rejected()
    {
        var (ok, err) = ODataSanitizer.ValidateUrlSuffix("path\\to\\file");
        Assert.False(ok);
        Assert.Contains("backslash", err);
    }

    [Fact]
    public void ValidateUrlSuffix_Empty_Rejected()
    {
        var (ok, _) = ODataSanitizer.ValidateUrlSuffix("");
        Assert.False(ok);
    }

    #endregion

    #region ValidateAll

    [Fact]
    public void ValidateAll_AllValid_ReturnsNull()
    {
        var error = ODataSanitizer.ValidateAll("IDocuments", "Name eq 'Test'", "Id,Name", "Created desc", "Author");
        Assert.Null(error);
    }

    [Fact]
    public void ValidateAll_InvalidEntitySet_ReturnsError()
    {
        var error = ODataSanitizer.ValidateAll("DROP TABLE", null, null, null, null);
        Assert.NotNull(error);
        Assert.Contains("Invalid entity set", error);
    }

    [Fact]
    public void ValidateAll_InvalidFilter_ReturnsError()
    {
        var error = ODataSanitizer.ValidateAll("IDocuments", "test; DROP TABLE", null, null, null);
        Assert.NotNull(error);
    }

    #endregion
}
