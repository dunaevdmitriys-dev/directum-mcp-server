using System.Text.Json;
using DirectumMcp.RuntimeTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ProcessStatsToolTests
{
    [Fact]
    public void FormatReport_EmptyData_ShowsNoData()
    {
        var result = ProcessStatsTool.FormatReport([], [], [], 30, "type", 15);
        Assert.Contains("Нет данных", result);
    }

    [Fact]
    public void FormatReport_WithAssignments_ShowsMetrics()
    {
        var assignmentsJson = """
        [
            {"Id":1, "Subject":"Согласование - Договор №1", "Created":"2026-03-01T08:00:00", "Completed":"2026-03-01T16:00:00", "Deadline":"2026-03-02T00:00:00", "Performer":{"Name":"Alice"}},
            {"Id":2, "Subject":"Рассмотрение - Приказ №2", "Created":"2026-03-02T08:00:00", "Completed":"2026-03-03T08:00:00", "Deadline":"2026-03-02T12:00:00", "Performer":{"Name":"Bob"}},
            {"Id":3, "Subject":"Согласование - Договор №3", "Created":"2026-03-03T08:00:00", "Completed":"2026-03-03T12:00:00", "Deadline":"2026-03-04T00:00:00", "Performer":{"Name":"Alice"}}
        ]
        """;
        using var assignDoc = JsonDocument.Parse(assignmentsJson);
        var assignments = assignDoc.RootElement.EnumerateArray().ToList();

        var tasksJson = """[{"Id":1, "Subject":"Task 1", "Created":"2026-03-01T08:00:00", "Started":"2026-03-01T08:00:00", "MaxDeadline":"2026-03-05T00:00:00", "Status":"Completed", "Author":{"Name":"Admin"}}]""";
        using var taskDoc = JsonDocument.Parse(tasksJson);
        var tasks = taskDoc.RootElement.EnumerateArray().ToList();

        var result = ProcessStatsTool.FormatReport(tasks, assignments, [], 30, "type", 15);

        Assert.Contains("Статистика процессов", result);
        Assert.Contains("Общие метрики", result);
        Assert.Contains("По типам задач", result);
        Assert.Contains("Согласование", result);
    }

    [Fact]
    public void FormatReport_GroupByPerformer_ShowsPerformers()
    {
        var json = """
        [
            {"Id":1, "Subject":"Task 1", "Created":"2026-03-01T08:00:00", "Completed":"2026-03-01T16:00:00", "Deadline":"2026-03-02T00:00:00", "Performer":{"Name":"Alice"}},
            {"Id":2, "Subject":"Task 2", "Created":"2026-03-02T08:00:00", "Completed":"2026-03-02T20:00:00", "Deadline":"2026-03-03T00:00:00", "Performer":{"Name":"Bob"}}
        ]
        """;
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();

        var result = ProcessStatsTool.FormatReport([], items, [], 30, "performer", 15);

        Assert.Contains("По исполнителям", result);
        Assert.Contains("Alice", result);
        Assert.Contains("Bob", result);
    }

    [Fact]
    public void FormatReport_GroupByWeekday_ShowsDays()
    {
        var json = """
        [
            {"Id":1, "Subject":"Task", "Created":"2026-03-16T08:00:00", "Completed":"2026-03-16T16:00:00", "Deadline":"2026-03-17T00:00:00", "Performer":{"Name":"Alice"}}
        ]
        """;
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.EnumerateArray().ToList();

        var result = ProcessStatsTool.FormatReport([], items, [], 30, "weekday", 15);

        Assert.Contains("По дням недели", result);
        Assert.Contains("Пн", result);
    }
}
