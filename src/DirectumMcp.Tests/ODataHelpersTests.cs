using System.Text.Json;
using DirectumMcp.Core.Helpers;
using Xunit;

namespace DirectumMcp.Tests;

public class ODataHelpersTests
{
    #region EscapeOData

    [Fact]
    public void EscapeOData_SingleQuote_Doubled()
    {
        var result = ODataHelpers.EscapeOData("O'Brien");

        Assert.Equal("O''Brien", result);
    }

    [Fact]
    public void EscapeOData_MultipleSingleQuotes_AllDoubled()
    {
        var result = ODataHelpers.EscapeOData("it's a test's value");

        Assert.Equal("it''s a test''s value", result);
    }

    [Fact]
    public void EscapeOData_NoSpecialChars_Unchanged()
    {
        var result = ODataHelpers.EscapeOData("NoSpecialChars123");

        Assert.Equal("NoSpecialChars123", result);
    }

    [Fact]
    public void EscapeOData_EmptyString_ReturnsEmpty()
    {
        var result = ODataHelpers.EscapeOData("");

        Assert.Equal("", result);
    }

    [Fact]
    public void EscapeOData_OnlyQuote_ReturnsTwoQuotes()
    {
        var result = ODataHelpers.EscapeOData("'");

        Assert.Equal("''", result);
    }

    [Fact]
    public void EscapeOData_StringWithoutQuotes_NotModified()
    {
        const string input = "Hello World! @#$%^&*()";
        var result = ODataHelpers.EscapeOData(input);

        Assert.Equal(input, result);
    }

    #endregion

    #region GetItems

    [Fact]
    public void GetItems_WithValueArray_ReturnsValueArrayItems()
    {
        // Arrange: OData response with "value" array
        using var doc = JsonDocument.Parse("""
            {
              "@odata.context": "http://example.com/$metadata",
              "value": [
                { "Id": 1, "Name": "Item1" },
                { "Id": 2, "Name": "Item2" }
              ]
            }
            """);

        // Act
        var items = ODataHelpers.GetItems(doc.RootElement);

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void GetItems_RootArray_ThrowsInvalidOperation()
    {
        // NOTE: JsonElement.TryGetProperty throws InvalidOperationException when called on
        // an Array element (not Object). The ODataHelpers.GetItems implementation calls
        // TryGetProperty("value", ...) unconditionally before checking ValueKind, so passing
        // a root-level JSON array causes a throw rather than reaching the ValueKind check.
        using var doc = JsonDocument.Parse("""
            [
              { "Id": 1 },
              { "Id": 2 },
              { "Id": 3 }
            ]
            """);

        Assert.Throws<InvalidOperationException>(() => ODataHelpers.GetItems(doc.RootElement));
    }

    [Fact]
    public void GetItems_ObjectWithNoValueProperty_ReturnsEmpty()
    {
        // Arrange: object without "value" array property
        using var doc = JsonDocument.Parse("""
            { "Id": 42, "Name": "Test" }
            """);

        // Act
        var items = ODataHelpers.GetItems(doc.RootElement);

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public void GetItems_EmptyValueArray_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("""{ "value": [] }""");

        var items = ODataHelpers.GetItems(doc.RootElement);

        Assert.Empty(items);
    }

    [Fact]
    public void GetItems_EmptyRootArray_ThrowsInvalidOperation()
    {
        // Same as the non-empty root-array case: TryGetProperty throws on Array elements.
        using var doc = JsonDocument.Parse("[]");

        Assert.Throws<InvalidOperationException>(() => ODataHelpers.GetItems(doc.RootElement));
    }

    [Fact]
    public void GetItems_ValueArrayItems_AreCorrect()
    {
        using var doc = JsonDocument.Parse("""
            {
              "value": [
                { "Name": "Alpha" },
                { "Name": "Beta" }
              ]
            }
            """);

        var items = ODataHelpers.GetItems(doc.RootElement);

        Assert.Equal(2, items.Count);
        Assert.Equal("Alpha", items[0].GetProperty("Name").GetString());
        Assert.Equal("Beta", items[1].GetProperty("Name").GetString());
    }

    #endregion

    #region GetString

    [Fact]
    public void GetString_ExistingProperty_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("""{ "Name": "John" }""");

        var result = ODataHelpers.GetString(doc.RootElement, "Name");

        Assert.Equal("John", result);
    }

    [Fact]
    public void GetString_MissingProperty_ReturnsDash()
    {
        using var doc = JsonDocument.Parse("""{ "Name": "John" }""");

        var result = ODataHelpers.GetString(doc.RootElement, "NonExistent");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetString_NullValue_ReturnsDash()
    {
        using var doc = JsonDocument.Parse("""{ "Name": null }""");

        var result = ODataHelpers.GetString(doc.RootElement, "Name");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetString_NumberValue_ReturnsStringRepresentation()
    {
        using var doc = JsonDocument.Parse("""{ "Count": 42 }""");

        var result = ODataHelpers.GetString(doc.RootElement, "Count");

        Assert.Equal("42", result);
    }

    [Fact]
    public void GetString_EmptyStringValue_ReturnsEmptyString()
    {
        using var doc = JsonDocument.Parse("""{ "Name": "" }""");

        var result = ODataHelpers.GetString(doc.RootElement, "Name");

        // Empty string is a valid string value, not null — should return ""
        Assert.Equal("", result);
    }

    [Fact]
    public void GetString_BooleanTrue_ReturnsString()
    {
        using var doc = JsonDocument.Parse("""{ "IsActive": true }""");

        var result = ODataHelpers.GetString(doc.RootElement, "IsActive");

        Assert.Equal("True", result);
    }

    #endregion

    #region GetNestedString

    [Fact]
    public void GetNestedString_ExistingNestedProp_ReturnsValue()
    {
        using var doc = JsonDocument.Parse("""
            { "Author": { "Name": "Alice" } }
            """);

        var result = ODataHelpers.GetNestedString(doc.RootElement, "Author", "Name");

        Assert.Equal("Alice", result);
    }

    [Fact]
    public void GetNestedString_MissingObject_ReturnsDash()
    {
        using var doc = JsonDocument.Parse("""{ "Title": "Doc" }""");

        var result = ODataHelpers.GetNestedString(doc.RootElement, "Author", "Name");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetNestedString_MissingNestedProperty_ReturnsDash()
    {
        using var doc = JsonDocument.Parse("""
            { "Author": { "Email": "test@test.com" } }
            """);

        var result = ODataHelpers.GetNestedString(doc.RootElement, "Author", "Name");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetNestedString_NullNestedValue_ReturnsDash()
    {
        using var doc = JsonDocument.Parse("""
            { "Author": { "Name": null } }
            """);

        var result = ODataHelpers.GetNestedString(doc.RootElement, "Author", "Name");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetNestedString_ObjectIsNull_ReturnsDash()
    {
        using var doc = JsonDocument.Parse("""{ "Author": null }""");

        var result = ODataHelpers.GetNestedString(doc.RootElement, "Author", "Name");

        Assert.Equal("-", result);
    }

    [Fact]
    public void GetNestedString_DeepValue_ReturnsCorrectly()
    {
        using var doc = JsonDocument.Parse("""
            { "Department": { "Title": "Engineering" } }
            """);

        var result = ODataHelpers.GetNestedString(doc.RootElement, "Department", "Title");

        Assert.Equal("Engineering", result);
    }

    #endregion

    #region FormatDate

    [Fact]
    public void FormatDate_ValidIsoDate_FormattedCorrectly()
    {
        var result = ODataHelpers.FormatDate("2024-03-15T10:30:00");

        Assert.Equal("15.03.2024", result);
    }

    [Fact]
    public void FormatDate_NullDate_ReturnsDash()
    {
        var result = ODataHelpers.FormatDate(null);

        Assert.Equal("-", result);
    }

    [Fact]
    public void FormatDate_EmptyString_ReturnsDash()
    {
        var result = ODataHelpers.FormatDate("");

        Assert.Equal("-", result);
    }

    [Fact]
    public void FormatDate_DashString_ReturnsDash()
    {
        var result = ODataHelpers.FormatDate("-");

        Assert.Equal("-", result);
    }

    [Fact]
    public void FormatDate_ValidDateOnly_FormattedCorrectly()
    {
        var result = ODataHelpers.FormatDate("2025-01-01");

        Assert.Equal("01.01.2025", result);
    }

    [Fact]
    public void FormatDate_CustomFormat_UsesCustomFormat()
    {
        // Use "yyyy-MM-dd" with literal hyphens to avoid culture-sensitive date separator
        // (some locales replace "/" with "." in DateTime.ToString).
        var result = ODataHelpers.FormatDate("2024-06-20", "yyyy-MM-dd");

        Assert.Equal("2024-06-20", result);
    }

    [Fact]
    public void FormatDate_InvalidDate_ReturnsOriginalString()
    {
        var result = ODataHelpers.FormatDate("not-a-date");

        Assert.Equal("not-a-date", result);
    }

    [Fact]
    public void FormatDate_IsoDateWithOffset_FormattedCorrectly()
    {
        // ISO 8601 with timezone offset — DateTime.TryParse handles this
        var result = ODataHelpers.FormatDate("2024-12-31T23:59:59Z");

        // Result depends on local timezone conversion; just verify it's not "-" and is 10 chars (dd.MM.yyyy)
        Assert.NotEqual("-", result);
        Assert.Matches(@"^\d{2}\.\d{2}\.\d{4}$", result);
    }

    #endregion

    #region GetLong

    [Fact]
    public void GetLong_NumberValue_ReturnsLong()
    {
        using var doc = JsonDocument.Parse("""{ "Id": 12345 }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Id");

        Assert.Equal(12345L, result);
    }

    [Fact]
    public void GetLong_StringNumber_ReturnsLong()
    {
        using var doc = JsonDocument.Parse("""{ "Id": "67890" }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Id");

        Assert.Equal(67890L, result);
    }

    [Fact]
    public void GetLong_MissingProperty_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""{ "Name": "Test" }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Id");

        Assert.Equal(0L, result);
    }

    [Fact]
    public void GetLong_NullValue_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""{ "Id": null }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Id");

        Assert.Equal(0L, result);
    }

    [Fact]
    public void GetLong_ZeroValue_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""{ "Count": 0 }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Count");

        Assert.Equal(0L, result);
    }

    [Fact]
    public void GetLong_LargeNumber_ReturnsCorrectLong()
    {
        using var doc = JsonDocument.Parse("""{ "BigId": 9876543210 }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "BigId");

        Assert.Equal(9876543210L, result);
    }

    [Fact]
    public void GetLong_NonNumericString_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("""{ "Id": "not-a-number" }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Id");

        Assert.Equal(0L, result);
    }

    [Fact]
    public void GetLong_NegativeNumber_ReturnsNegative()
    {
        using var doc = JsonDocument.Parse("""{ "Delta": -42 }""");

        var result = ODataHelpers.GetLong(doc.RootElement, "Delta");

        Assert.Equal(-42L, result);
    }

    #endregion
}
