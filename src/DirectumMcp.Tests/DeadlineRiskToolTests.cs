using System.Text.Json;
using DirectumMcp.RuntimeTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class DeadlineRiskToolTests
{
    [Fact]
    public void CalculatePerformerAverages_ValidData_ReturnsAverages()
    {
        var json = """
        [
            {"Id":1, "Created":"2026-01-01T08:00:00", "Completed":"2026-01-01T16:00:00", "Performer":{"Name":"Alice"}},
            {"Id":2, "Created":"2026-01-02T08:00:00", "Completed":"2026-01-02T20:00:00", "Performer":{"Name":"Alice"}},
            {"Id":3, "Created":"2026-01-01T08:00:00", "Completed":"2026-01-02T08:00:00", "Performer":{"Name":"Bob"}}
        ]
        """;
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();

        var result = DeadlineRiskTool.CalculatePerformerAverages(items);

        Assert.Equal(2, result.Count);
        Assert.Equal(10.0, result["Alice"], 0.1); // (8+12)/2 = 10
        Assert.Equal(24.0, result["Bob"], 0.1);
    }

    [Fact]
    public void CalculateWorkload_GroupsByPerformer()
    {
        var json = """
        [
            {"Performer":{"Name":"Alice"}},
            {"Performer":{"Name":"Alice"}},
            {"Performer":{"Name":"Bob"}}
        ]
        """;
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();

        var result = DeadlineRiskTool.CalculateWorkload(items);

        Assert.Equal(2, result["Alice"]);
        Assert.Equal(1, result["Bob"]);
    }

    [Fact]
    public void ScoreAssignments_OverdueItem_MarkedOverdue()
    {
        var pastDeadline = DateTime.UtcNow.AddHours(-10).ToString("o");
        var created = DateTime.UtcNow.AddDays(-2).ToString("o");
        var json = "[{\"Id\":1, \"Subject\":\"Test task\", \"Created\":\"" + created +
                   "\", \"Deadline\":\"" + pastDeadline +
                   "\", \"Importance\":\"Normal\", \"Performer\":{\"Name\":\"Alice\"}}]";
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();
        var avgHours = new Dictionary<string, double> { ["Alice"] = 24.0 };
        var workload = new Dictionary<string, int> { ["Alice"] = 1 };

        var risks = DeadlineRiskTool.ScoreAssignments(items, avgHours, workload);

        Assert.Single(risks);
        Assert.Contains("OVERDUE", risks[0].Risk);
    }

    [Fact]
    public void ScoreAssignments_FarDeadline_LowRisk()
    {
        var farDeadline = DateTime.UtcNow.AddDays(30).ToString("o");
        var created = DateTime.UtcNow.AddHours(-1).ToString("o");
        var json = "[{\"Id\":1, \"Subject\":\"Easy task\", \"Created\":\"" + created +
                   "\", \"Deadline\":\"" + farDeadline +
                   "\", \"Importance\":\"Normal\", \"Performer\":{\"Name\":\"Bob\"}}]";
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();
        var avgHours = new Dictionary<string, double> { ["Bob"] = 8.0 };
        var workload = new Dictionary<string, int> { ["Bob"] = 1 };

        var risks = DeadlineRiskTool.ScoreAssignments(items, avgHours, workload);

        Assert.Single(risks);
        Assert.Contains("Low", risks[0].Risk);
    }

    [Fact]
    public void FormatReport_EmptyRisks_ShowsNoAssignments()
    {
        var result = DeadlineRiskTool.FormatReport([], 20, 60, 5);

        Assert.Contains("Нет активных заданий", result);
    }

    [Fact]
    public void FormatReport_WithRisks_ContainsTable()
    {
        var risks = new List<RiskItem>
        {
            new(1, "Test task", "Alice", DateTime.UtcNow.AddHours(2), 2.0, 8.0, 3, "🔴 High", "Normal"),
            new(2, "Easy task", "Bob", DateTime.UtcNow.AddDays(5), 120.0, 10.0, 1, "🟢 Low", "Normal"),
        };

        var result = DeadlineRiskTool.FormatReport(risks, 20, 60, 5);

        Assert.Contains("Прогноз просрочки", result);
        Assert.Contains("Alice", result);
        Assert.Contains("Bob", result);
        Assert.Contains("Высокий риск", result);
    }
}
