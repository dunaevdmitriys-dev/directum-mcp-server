using DirectumMcp.RuntimeTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class AnalyticsToolsTests
{
    #region BottleneckDetectTool

    [Fact]
    public void Bottleneck_EmptyData_NoBottlenecks()
    {
        var result = BottleneckDetectTool.FormatReport(new List<AssignmentData>(), days: 30);

        Assert.Contains("Проанализировано заданий:** 0", result);
        Assert.Contains("Нет завершённых заданий", result);
    }

    [Fact]
    public void Bottleneck_SinglePerformer_ShowsStats()
    {
        var data = new List<AssignmentData>
        {
            new("Иванов И.И.", 8.0, false),
            new("Иванов И.И.", 12.0, true),
            new("Иванов И.И.", 10.0, false),
            new("Иванов И.И.", 6.0, false),
            new("Иванов И.И.", 4.0, false),
        };

        var result = BottleneckDetectTool.FormatReport(data, days: 30, minCount: 5, top: 10);

        Assert.Contains("Иванов И.И.", result);
        Assert.Contains("Среднее время выполнения", result);
        Assert.Contains("Медиана", result);
    }

    [Fact]
    public void Bottleneck_MultiplePerformers_SortedByAvgDuration()
    {
        var data = new List<AssignmentData>
        {
            // Петров: avg 20h
            new("Петров П.П.", 20.0, false),
            new("Петров П.П.", 20.0, false),
            new("Петров П.П.", 20.0, false),
            new("Петров П.П.", 20.0, false),
            new("Петров П.П.", 20.0, false),
            // Иванов: avg 5h
            new("Иванов И.И.", 5.0, false),
            new("Иванов И.И.", 5.0, false),
            new("Иванов И.И.", 5.0, false),
            new("Иванов И.И.", 5.0, false),
            new("Иванов И.И.", 5.0, false),
        };

        var result = BottleneckDetectTool.FormatReport(data, days: 30, minCount: 5, top: 10);

        // Петров (higher avg) should appear before Иванов
        var petrovIndex = result.IndexOf("Петров П.П.", StringComparison.Ordinal);
        var ivanovIndex = result.IndexOf("Иванов И.И.", StringComparison.Ordinal);
        Assert.True(petrovIndex < ivanovIndex, "Петров (slower) should appear before Иванов in sorted output");
    }

    [Fact]
    public void Bottleneck_MinCountFilter_ExcludesSmallGroups()
    {
        var data = new List<AssignmentData>
        {
            new("Редкий Р.Р.", 100.0, false), // only 1 — below minCount=5
            new("Частый Ч.Ч.", 5.0, false),
            new("Частый Ч.Ч.", 5.0, false),
            new("Частый Ч.Ч.", 5.0, false),
            new("Частый Ч.Ч.", 5.0, false),
            new("Частый Ч.Ч.", 5.0, false),
        };

        var result = BottleneckDetectTool.FormatReport(data, days: 30, minCount: 5, top: 10);

        // Редкий has only 1 assignment and should be excluded from the performer table
        // but total analyzed count should still be 6
        Assert.Contains("Проанализировано заданий:** 6", result);
        Assert.DoesNotContain("Редкий Р.Р.", result);
        Assert.Contains("Частый Ч.Ч.", result);
    }

    #endregion

    #region OverdueReportTool

    [Fact]
    public void Overdue_EmptyList_NoOverdue()
    {
        var result = OverdueReportTool.FormatReport(new List<OverdueItem>(), groupBy: "performer");

        Assert.Contains("Всего просрочено:** 0", result);
        Assert.Contains("Просроченных заданий не найдено", result);
    }

    [Fact]
    public void Overdue_GroupByPerformer_GroupedCorrectly()
    {
        var deadline = DateTime.UtcNow.AddDays(-2);
        var items = new List<OverdueItem>
        {
            new(1, "Задание 1", "Иванов И.И.", "Автор А.А.", "Normal", deadline, 2.0),
            new(2, "Задание 2", "Иванов И.И.", "Автор А.А.", "High", deadline, 3.0),
            new(3, "Задание 3", "Петров П.П.", "Автор Б.Б.", "Normal", deadline, 1.0),
        };

        var result = OverdueReportTool.FormatReport(items, groupBy: "performer");

        Assert.Contains("По исполнителям", result);
        Assert.Contains("Иванов И.И. (2 заданий)", result);
        Assert.Contains("Петров П.П. (1 заданий)", result);
    }

    [Fact]
    public void Overdue_GroupByImportance_GroupedCorrectly()
    {
        var deadline = DateTime.UtcNow.AddDays(-1);
        var items = new List<OverdueItem>
        {
            new(1, "Задание 1", "Иванов И.И.", "Автор А.А.", "High", deadline, 1.0),
            new(2, "Задание 2", "Петров П.П.", "Автор Б.Б.", "High", deadline, 2.0),
            new(3, "Задание 3", "Сидоров С.С.", "Автор В.В.", "Normal", deadline, 1.5),
        };

        var result = OverdueReportTool.FormatReport(items, groupBy: "importance");

        Assert.Contains("По важности", result);
        Assert.Contains("High (2 заданий)", result);
        Assert.Contains("Normal (1 заданий)", result);
    }

    [Fact]
    public void Overdue_GroupByAuthor_GroupedCorrectly()
    {
        var deadline = DateTime.UtcNow.AddDays(-1);
        var items = new List<OverdueItem>
        {
            new(1, "Задание 1", "Иванов И.И.", "Директор Д.Д.", "Normal", deadline, 1.0),
            new(2, "Задание 2", "Петров П.П.", "Директор Д.Д.", "High", deadline, 2.0),
        };

        var result = OverdueReportTool.FormatReport(items, groupBy: "author");

        Assert.Contains("По авторам", result);
        Assert.Contains("Директор Д.Д. (2 заданий)", result);
    }

    [Fact]
    public void Overdue_ContainsCompleteHint()
    {
        var deadline = DateTime.UtcNow.AddDays(-1);
        var items = new List<OverdueItem>
        {
            new(42, "Тест", "Иванов И.И.", "Автор А.А.", "Normal", deadline, 1.0),
        };

        var result = OverdueReportTool.FormatReport(items, groupBy: "performer");

        Assert.Contains("complete assignmentId=<ID>", result);
    }

    #endregion

    #region TeamWorkloadTool

    [Fact]
    public void Workload_EmptyList_NoData()
    {
        var result = TeamWorkloadTool.FormatReport(new List<WorkloadItem>());

        Assert.Contains("Активных заданий:** 0", result);
        Assert.Contains("Исполнителей:** 0", result);
        Assert.Contains("Активных заданий не найдено", result);
    }

    [Fact]
    public void Workload_SinglePerformer_ShowsBar()
    {
        var items = new List<WorkloadItem>
        {
            new("Иванов И.И.", 10, 2, 3)
        };

        var result = TeamWorkloadTool.FormatReport(items);

        Assert.Contains("Иванов И.И.", result);
        Assert.Contains("10", result);
        Assert.Contains("Загрузка", result);
        // Single performer → max=10 → filled=10 → all filled
        Assert.Contains("██████████", result);
    }

    [Fact]
    public void Workload_MultiplePerformers_SortedByTotal()
    {
        var items = new List<WorkloadItem>
        {
            new("Иванов И.И.", 5, 0, 0),
            new("Петров П.П.", 15, 2, 3),
            new("Сидоров С.С.", 10, 1, 1),
        };

        var result = TeamWorkloadTool.FormatReport(items);

        var ivanovIndex = result.IndexOf("Иванов И.И.", StringComparison.Ordinal);
        var petrovIndex = result.IndexOf("Петров П.П.", StringComparison.Ordinal);
        var sidorovIndex = result.IndexOf("Сидоров С.С.", StringComparison.Ordinal);

        // Already sorted descending before FormatReport is called (BuildWorkload sorts)
        // FormatReport just renders in given order — Петров first (15), Сидоров (10), Иванов (5)
        Assert.True(petrovIndex < sidorovIndex);
        Assert.True(sidorovIndex < ivanovIndex);
    }

    [Fact]
    public void Workload_BarVisualization_CorrectLength()
    {
        // bar should always be 10 characters
        for (int value = 0; value <= 10; value++)
        {
            var bar = TeamWorkloadTool.BuildBar(value, 10);
            Assert.Equal(10, bar.Length);
        }
    }

    [Fact]
    public void Workload_BarVisualization_MaxValueIsFullBar()
    {
        var bar = TeamWorkloadTool.BuildBar(10, 10);
        Assert.Equal("██████████", bar);
    }

    [Fact]
    public void Workload_BarVisualization_ZeroValueIsEmptyBar()
    {
        var bar = TeamWorkloadTool.BuildBar(0, 10);
        Assert.Equal("░░░░░░░░░░", bar);
    }

    [Fact]
    public void Workload_ShowsAverageLoad()
    {
        var items = new List<WorkloadItem>
        {
            new("Иванов И.И.", 10, 0, 0),
            new("Петров П.П.", 20, 0, 0),
        };

        var result = TeamWorkloadTool.FormatReport(items);

        Assert.Contains("Средняя нагрузка:", result);
        Assert.Contains("15", result); // avg of 10 and 20
    }

    #endregion
}
