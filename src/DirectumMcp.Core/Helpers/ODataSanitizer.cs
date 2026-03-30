using System.Text.RegularExpressions;

namespace DirectumMcp.Core.Helpers;

/// <summary>
/// Validates and sanitizes OData query parameters to prevent injection attacks.
/// </summary>
public static partial class ODataSanitizer
{
    // Dangerous patterns that should never appear in OData parameters
    private static readonly string[] DangerousPatterns =
    [
        "--",       // SQL comment
        "/*",       // Block comment
        "*/",
        ";",        // Statement separator
        "UNION",    // SQL UNION
        "DROP ",    // DDL
        "DELETE ",  // DML
        "INSERT ",
        "UPDATE ",
        "EXEC ",
        "EXECUTE ",
        "xp_",     // Extended stored procedures
        "sp_",     // System stored procedures
        "0x",      // Hex literals (potential bypass)
    ];

    /// <summary>
    /// Allowed characters in OData entity set names: letters, digits, underscore, dot.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_\.]*$")]
    private static partial Regex EntitySetNameRegex();

    /// <summary>
    /// Allowed characters in $select/$expand: letters, digits, underscore, comma, space, dot, slash.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_,\s\.\/$\(\)]+$")]
    private static partial Regex SelectExpandRegex();

    /// <summary>
    /// Allowed in $orderby: letters, digits, underscore, comma, space, dot, "asc", "desc".
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_,\s\.]+$")]
    private static partial Regex OrderByRegex();

    /// <summary>
    /// Allowed in URL suffix for GetRawAsync: letters, digits, common OData chars.
    /// No backslashes, no "..", no control characters.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_\.\-/\?\&\=\$\(\),\+\s\':@%]+$")]
    private static partial Regex UrlSuffixRegex();

    /// <summary>
    /// Validates an OData entity set name.
    /// </summary>
    public static bool IsValidEntitySet(string entitySet)
    {
        if (string.IsNullOrWhiteSpace(entitySet) || entitySet.Length > 200)
            return false;
        return EntitySetNameRegex().IsMatch(entitySet);
    }

    /// <summary>
    /// Validates a $filter expression for dangerous patterns.
    /// OData filters are complex (function calls, nested expressions) so we check for known-bad patterns
    /// rather than trying to whitelist all valid syntax.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return (true, null);

        if (filter.Length > 2000)
            return (false, "Filter expression too long (max 2000 characters).");

        var upper = filter.ToUpperInvariant();
        foreach (var pattern in DangerousPatterns)
        {
            if (upper.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return (false, $"Filter contains potentially dangerous pattern: '{pattern}'");
        }

        // Check for unbalanced quotes (potential injection)
        var singleQuotes = filter.Count(c => c == '\'');
        if (singleQuotes % 2 != 0)
            return (false, "Filter contains unbalanced single quotes.");

        return (true, null);
    }

    /// <summary>
    /// Validates $select or $expand parameter.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateSelectExpand(string? value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
            return (true, null);

        if (value.Length > 500)
            return (false, $"{paramName} too long (max 500 characters).");

        if (!SelectExpandRegex().IsMatch(value))
            return (false, $"{paramName} contains invalid characters. Allowed: letters, digits, underscore, comma, dot.");

        return CheckDangerousPatterns(value, paramName);
    }

    /// <summary>
    /// Validates $orderby parameter.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateOrderBy(string? orderby)
    {
        if (string.IsNullOrWhiteSpace(orderby))
            return (true, null);

        if (orderby.Length > 300)
            return (false, "$orderby too long (max 300 characters).");

        if (!OrderByRegex().IsMatch(orderby))
            return (false, "$orderby contains invalid characters.");

        return CheckDangerousPatterns(orderby, "$orderby");
    }

    /// <summary>
    /// Validates a raw URL suffix for GetRawAsync.
    /// </summary>
    public static (bool IsValid, string? Error) ValidateUrlSuffix(string? suffix)
    {
        if (string.IsNullOrWhiteSpace(suffix))
            return (false, "URL suffix cannot be empty.");

        if (suffix.Length > 2000)
            return (false, "URL suffix too long (max 2000 characters).");

        if (suffix.Contains(".."))
            return (false, "URL suffix contains path traversal pattern '..'");

        if (suffix.Contains('\\'))
            return (false, "URL suffix contains backslash.");

        if (!UrlSuffixRegex().IsMatch(suffix))
            return (false, "URL suffix contains invalid characters.");

        return CheckDangerousPatterns(suffix, "URL suffix");
    }

    /// <summary>
    /// Validates all OData query parameters at once. Returns first error found or null.
    /// </summary>
    public static string? ValidateAll(string? entitySet, string? filter, string? select,
        string? orderby, string? expand)
    {
        if (entitySet is not null && !IsValidEntitySet(entitySet))
            return $"Invalid entity set name: '{entitySet}'";

        var (filterOk, filterErr) = ValidateFilter(filter);
        if (!filterOk) return filterErr;

        var (selectOk, selectErr) = ValidateSelectExpand(select, "$select");
        if (!selectOk) return selectErr;

        var (expandOk, expandErr) = ValidateSelectExpand(expand, "$expand");
        if (!expandOk) return expandErr;

        var (orderOk, orderErr) = ValidateOrderBy(orderby);
        if (!orderOk) return orderErr;

        return null;
    }

    private static (bool IsValid, string? Error) CheckDangerousPatterns(string value, string paramName)
    {
        var upper = value.ToUpperInvariant();
        foreach (var pattern in DangerousPatterns)
        {
            if (upper.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return (false, $"{paramName} contains potentially dangerous pattern: '{pattern}'");
        }
        return (true, null);
    }
}
