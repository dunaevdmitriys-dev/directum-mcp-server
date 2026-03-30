using System.Text.Json;
using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ValidateWorkflowToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly ValidateWorkflowTool _tool;

    public ValidateWorkflowToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WfTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new ValidateWorkflowTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ---------- Helpers ----------

    private string WriteMtd(string fileName, string json)
    {
        var path = Path.Combine(_tempDir, fileName);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    private void WriteHandlers(string dirPath, string content)
    {
        Directory.CreateDirectory(dirPath);
        File.WriteAllText(Path.Combine(dirPath, "TaskRouteHandlers.cs"), content);
    }

    private static List<ValidateWorkflowTool.RouteBlock> ParseBlocks(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return ValidateWorkflowTool.ExtractBlocks(doc.RootElement);
    }

    /// <summary>Builds a minimal task MTD JSON with RouteScheme.Blocks.</summary>
    private static string BuildMtd(params object[] blockDefs)
    {
        // blockDefs: anonymous objects converted via JSON
        var blocksJson = string.Join(",\n", blockDefs.Select(b =>
            System.Text.Json.JsonSerializer.Serialize(b)));
        return $$"""
            {
              "$type": "Sungero.Metadata.TaskMetadata",
              "NameGuid": "aaaaaaaa-0000-0000-0000-000000000001",
              "Name": "TestTask",
              "RouteScheme": {
                "Blocks": [
                  {{blocksJson}}
                ]
              }
            }
            """;
    }

    // ---------- ExtractBlocks ----------

    [Fact]
    public void ExtractBlocks_RouteSchemeBlocks_ParsedCorrectly()
    {
        var json = BuildMtd(new
        {
            NameGuid = "block1-0000-0000-0000-000000000001",
            Name = "StartBlock",
            BlockType = "StartBlock",
            GenerateHandler = false,
            Connectors = new[] { new { ToBlock = "block2-0000-0000-0000-000000000001", Condition = "" } }
        });

        var blocks = ParseBlocks(json);

        Assert.Single(blocks);
        Assert.Equal("StartBlock", blocks[0].Name);
        Assert.Equal("StartBlock", blocks[0].BlockType);
        Assert.Single(blocks[0].Connectors);
        Assert.Equal("block2-0000-0000-0000-000000000001", blocks[0].Connectors[0].ToBlock);
    }

    [Fact]
    public void ExtractBlocks_RootLevelBlocks_ParsedWhenNoRouteScheme()
    {
        var json = """
            {
              "$type": "Sungero.Metadata.TaskMetadata",
              "NameGuid": "task-0000-0000-0000-000000000001",
              "Name": "FlatTask",
              "Blocks": [
                {
                  "NameGuid": "b1000000-0000-0000-0000-000000000001",
                  "Name": "StartBlock",
                  "BlockType": "StartBlock",
                  "GenerateHandler": false,
                  "Connectors": []
                }
              ]
            }
            """;

        var blocks = ParseBlocks(json);

        Assert.Single(blocks);
        Assert.Equal("StartBlock", blocks[0].Name);
    }

    [Fact]
    public void ExtractBlocks_NoBlocks_ReturnsEmpty()
    {
        var json = """
            {
              "$type": "Sungero.Metadata.EntityMetadata",
              "NameGuid": "e1000000-0000-0000-0000-000000000001",
              "Name": "SomeEntity"
            }
            """;

        var blocks = ParseBlocks(json);

        Assert.Empty(blocks);
    }

    // ---------- Check 1: Dead blocks ----------

    [Fact]
    public void Check1_DeadBlocks_AllReachable_NoIssues()
    {
        // StartBlock -> ApproveBlock -> EndBlock (linear chain, all reachable)
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("start-guid", "StartBlock", "StartBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector> { new("approve-guid", "") }),
            new("approve-guid", "ApproveBlock", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector> { new("end-guid", "") }),
            new("end-guid", "EndBlock", "EndBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check1_DeadBlocks(blocks).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Check1_DeadBlocks_UnreachableBlock_ReturnsError()
    {
        // StartBlock -> EndBlock (DeadBlock is orphaned)
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("start-guid", "StartBlock", "StartBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector> { new("end-guid", "") }),
            new("end-guid", "EndBlock", "EndBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>()),
            new("dead-guid", "DeadBlock", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check1_DeadBlocks(blocks).ToList();

        Assert.Single(issues);
        Assert.Equal(ValidateWorkflowTool.IssueSeverity.Error, issues[0].Severity);
        Assert.Equal("DeadBlocks", issues[0].CheckName);
        Assert.Contains("DeadBlock", issues[0].Message);
    }

    [Fact]
    public void Check1_DeadBlocks_NoStartBlock_ReturnsWarning()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("block-guid", "SomeBlock", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check1_DeadBlocks(blocks).ToList();

        Assert.Single(issues);
        Assert.Equal(ValidateWorkflowTool.IssueSeverity.Warning, issues[0].Severity);
        Assert.Contains("StartBlock не найден", issues[0].Message);
    }

    [Fact]
    public void Check1_DeadBlocks_MultipleDeadBlocks_AllFlagged()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("start-guid", "StartBlock", "StartBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector> { new("end-guid", "") }),
            new("end-guid", "EndBlock", "EndBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>()),
            new("dead1-guid", "Dead1", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>()),
            new("dead2-guid", "Dead2", "SignBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check1_DeadBlocks(blocks).ToList();

        Assert.Equal(2, issues.Count);
        Assert.All(issues, i => Assert.Equal(ValidateWorkflowTool.IssueSeverity.Error, i.Severity));
        Assert.Contains(issues, i => i.Message.Contains("Dead1"));
        Assert.Contains(issues, i => i.Message.Contains("Dead2"));
    }

    // ---------- Check 2: ConditionBlock without conditions ----------

    [Fact]
    public void Check2_ConditionBlock_WithConditions_NoIssues()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("cond-guid", "CheckApproval", "ConditionBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>
                {
                    new("yes-guid", "Approved"),
                    new("no-guid", "Rejected")
                })
        };

        var issues = ValidateWorkflowTool.Check2_ConditionBlocksWithoutConditions(blocks).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Check2_ConditionBlock_WithoutConditions_ReturnsWarning()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("cond-guid", "CheckApproval", "ConditionBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>
                {
                    new("yes-guid", ""),   // empty condition
                    new("no-guid", "")     // empty condition
                })
        };

        var issues = ValidateWorkflowTool.Check2_ConditionBlocksWithoutConditions(blocks).ToList();

        Assert.Single(issues);
        Assert.Equal(ValidateWorkflowTool.IssueSeverity.Warning, issues[0].Severity);
        Assert.Equal("ConditionWithoutConditions", issues[0].CheckName);
        Assert.Contains("CheckApproval", issues[0].Message);
    }

    [Fact]
    public void Check2_NonConditionBlock_NotFlagged()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("review-guid", "ReviewBlock", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>
                {
                    new("end-guid", "")
                })
        };

        var issues = ValidateWorkflowTool.Check2_ConditionBlocksWithoutConditions(blocks).ToList();

        Assert.Empty(issues);
    }

    // ---------- Check 3: Handlers without code ----------

    [Fact]
    public void Check3_GenerateHandlerTrue_MethodExists_NoIssues()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("rev-guid", "ReviewDocument", "ReviewBlock", "", true,
                new List<ValidateWorkflowTool.RouteConnector>())
        };
        var handlerContent = """
            public partial class TestTask
            {
                public void ReviewDocument(IReviewDocumentBlockEvents e) { }
            }
            """;

        var issues = ValidateWorkflowTool.Check3_HandlersWithoutCode(blocks, handlerContent).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Check3_GenerateHandlerTrue_MethodMissing_ReturnsWarning()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("rev-guid", "ReviewDocument", "ReviewBlock", "", true,
                new List<ValidateWorkflowTool.RouteConnector>())
        };
        var handlerContent = """
            public partial class TestTask
            {
                public void SomeOtherMethod() { }
            }
            """;

        var issues = ValidateWorkflowTool.Check3_HandlersWithoutCode(blocks, handlerContent).ToList();

        Assert.Single(issues);
        Assert.Equal(ValidateWorkflowTool.IssueSeverity.Warning, issues[0].Severity);
        Assert.Equal("HandlerWithoutCode", issues[0].CheckName);
        Assert.Contains("ReviewDocument", issues[0].Message);
    }

    [Fact]
    public void Check3_GenerateHandlerTrue_NoHandlerFile_ReturnsWarning()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("rev-guid", "ReviewDocument", "ReviewBlock", "", true,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check3_HandlersWithoutCode(blocks, null).ToList();

        Assert.Single(issues);
        Assert.Equal(ValidateWorkflowTool.IssueSeverity.Warning, issues[0].Severity);
        Assert.Contains("RouteHandlers.cs не найден", issues[0].Message);
    }

    [Fact]
    public void Check3_GenerateHandlerFalse_NoIssues()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("rev-guid", "ReviewDocument", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check3_HandlersWithoutCode(blocks, null).ToList();

        Assert.Empty(issues);
    }

    // ---------- Check 4: Empty blocks (dead ends) ----------

    [Fact]
    public void Check4_EmptyBlocks_NormalBlockWithConnectors_NoIssues()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("rev-guid", "ReviewBlock", "ReviewBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector> { new("end-guid", "") })
        };

        var issues = ValidateWorkflowTool.Check4_EmptyBlocks(blocks).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Check4_EmptyBlocks_EndBlockWithNoConnectors_NotFlagged()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("end-guid", "EndBlock", "EndBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check4_EmptyBlocks(blocks).ToList();

        Assert.Empty(issues);
    }

    [Fact]
    public void Check4_EmptyBlocks_NonEndBlockNoConnectors_ReturnsWarning()
    {
        var blocks = new List<ValidateWorkflowTool.RouteBlock>
        {
            new("sign-guid", "SignBlock", "SignBlock", "", false,
                new List<ValidateWorkflowTool.RouteConnector>())
        };

        var issues = ValidateWorkflowTool.Check4_EmptyBlocks(blocks).ToList();

        Assert.Single(issues);
        Assert.Equal(ValidateWorkflowTool.IssueSeverity.Warning, issues[0].Severity);
        Assert.Equal("EmptyBlock", issues[0].CheckName);
        Assert.Contains("SignBlock", issues[0].Message);
    }

    // ---------- Integration: full tool invocation ----------

    [Fact]
    public async Task ValidateWorkflow_CleanScheme_NoIssuesInReport()
    {
        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        WriteMtd("CleanTask.mtd", mtdJson);

        var result = await _tool.ValidateWorkflow(_tempDir);

        Assert.Contains("Проблем не обнаружено", result);
    }

    [Fact]
    public async Task ValidateWorkflow_DeadBlock_ReportedInOutput()
    {
        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            },
            new
            {
                NameGuid = "b3000000-0000-0000-0000-000000000001",
                Name = "OrphanBlock",
                BlockType = "ReviewBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        WriteMtd("DeadBlockTask.mtd", mtdJson);

        var result = await _tool.ValidateWorkflow(_tempDir);

        Assert.Contains("DeadBlocks", result);
        Assert.Contains("OrphanBlock", result);
    }

    [Fact]
    public async Task ValidateWorkflow_PathOutsideSolutionPath_ReturnsDenied()
    {
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = Path.GetPathRoot(Path.GetTempPath()) ?? "C:\\Windows";

        var result = await _tool.ValidateWorkflow(outsidePath);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    [Fact]
    public async Task ValidateWorkflow_NonexistentPath_ReturnsError()
    {
        var result = await _tool.ValidateWorkflow(Path.Combine(_tempDir, "no_such_dir"));

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("не найдена", result.Replace("не найден", "не найдена"));
    }

    [Fact]
    public async Task ValidateWorkflow_SeverityFilterError_OnlyErrors()
    {
        // Schema with a dead block (error) and an empty non-end block (warning)
        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            },
            new
            {
                NameGuid = "b3000000-0000-0000-0000-000000000001",
                Name = "OrphanBlock",
                BlockType = "ReviewBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        WriteMtd("FilterTask.mtd", mtdJson);

        var result = await _tool.ValidateWorkflow(_tempDir, "error");

        // Dead block is an error — must appear
        Assert.Contains("DeadBlocks", result);
        // EmptyBlock is a warning — must not appear when filtered to errors only
        Assert.DoesNotContain("EmptyBlock", result);
    }

    [Fact]
    public async Task ValidateWorkflow_WithRouteHandlersFile_HandlerFound_NoHandlerIssue()
    {
        var subDir = Path.Combine(_tempDir, "TaskWithHandler");
        Directory.CreateDirectory(subDir);

        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "ReviewAndSign",
                BlockType = "ReviewBlock",
                GenerateHandler = true,
                Connectors = new[] { new { ToBlock = "b3000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b3000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        File.WriteAllText(Path.Combine(subDir, "MyTask.mtd"), mtdJson);

        // Handler file WITH the correct method
        File.WriteAllText(Path.Combine(subDir, "MyTaskRouteHandlers.cs"), """
            public partial class MyTask
            {
                public void ReviewAndSign(IReviewAndSignBlockEvents e) { }
            }
            """);

        var result = await _tool.ValidateWorkflow(subDir);

        Assert.DoesNotContain("HandlerWithoutCode", result);
    }

    [Fact]
    public async Task ValidateWorkflow_WithRouteHandlersFile_HandlerMissing_ReportsWarning()
    {
        var subDir = Path.Combine(_tempDir, "TaskMissingHandler");
        Directory.CreateDirectory(subDir);

        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "SignBlock",
                BlockType = "SignBlock",
                GenerateHandler = true,
                Connectors = new[] { new { ToBlock = "b3000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b3000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        File.WriteAllText(Path.Combine(subDir, "MyTask.mtd"), mtdJson);

        // Handler file WITHOUT the SignBlock method
        File.WriteAllText(Path.Combine(subDir, "MyTaskRouteHandlers.cs"), """
            public partial class MyTask
            {
                public void SomeUnrelatedMethod() { }
            }
            """);

        var result = await _tool.ValidateWorkflow(subDir);

        Assert.Contains("HandlerWithoutCode", result);
        Assert.Contains("SignBlock", result);
    }

    [Fact]
    public async Task ValidateWorkflow_ConditionBlockNoConditions_ReportedInOutput()
    {
        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "CheckCondition",
                BlockType = "ConditionBlock",
                GenerateHandler = false,
                Connectors = new[]
                {
                    new { ToBlock = "b3000000-0000-0000-0000-000000000001", Condition = "" },
                    new { ToBlock = "b4000000-0000-0000-0000-000000000001", Condition = "" }
                }
            },
            new
            {
                NameGuid = "b3000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            },
            new
            {
                NameGuid = "b4000000-0000-0000-0000-000000000001",
                Name = "EndBlock2",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        WriteMtd("ConditionTask.mtd", mtdJson);

        var result = await _tool.ValidateWorkflow(_tempDir);

        Assert.Contains("ConditionWithoutConditions", result);
        Assert.Contains("CheckCondition", result);
    }

    [Fact]
    public async Task ValidateWorkflow_NoMtdFiles_ReturnsError()
    {
        var emptyDir = Path.Combine(_tempDir, "empty_sub");
        Directory.CreateDirectory(emptyDir);

        var result = await _tool.ValidateWorkflow(emptyDir);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains(".mtd", result);
    }

    [Fact]
    public async Task ValidateWorkflow_DirectMtdFilePath_Works()
    {
        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        var mtdPath = WriteMtd("DirectTask.mtd", mtdJson);

        var result = await _tool.ValidateWorkflow(mtdPath);

        // File is clean — no issues
        Assert.Contains("Проблем не обнаружено", result);
    }

    [Fact]
    public async Task ValidateWorkflow_SeverityFilterWarning_OnlyWarnings()
    {
        // Schema with a dead block (error) AND a condition block without conditions (warning)
        var mtdJson = BuildMtd(
            new
            {
                NameGuid = "b1000000-0000-0000-0000-000000000001",
                Name = "StartBlock",
                BlockType = "StartBlock",
                GenerateHandler = false,
                Connectors = new[] { new { ToBlock = "b2000000-0000-0000-0000-000000000001", Condition = "" } }
            },
            new
            {
                NameGuid = "b2000000-0000-0000-0000-000000000001",
                Name = "CheckCondition",
                BlockType = "ConditionBlock",
                GenerateHandler = false,
                Connectors = new[]
                {
                    new { ToBlock = "b3000000-0000-0000-0000-000000000001", Condition = "" }
                }
            },
            new
            {
                NameGuid = "b3000000-0000-0000-0000-000000000001",
                Name = "EndBlock",
                BlockType = "EndBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            },
            new
            {
                NameGuid = "b4000000-0000-0000-0000-000000000001",
                Name = "OrphanBlock",
                BlockType = "ReviewBlock",
                GenerateHandler = false,
                Connectors = Array.Empty<object>()
            });

        WriteMtd("SeverityTask.mtd", mtdJson);

        var result = await _tool.ValidateWorkflow(_tempDir, "warning");

        // Warning: ConditionWithoutConditions should appear
        Assert.Contains("ConditionWithoutConditions", result);
        // Error: DeadBlocks should NOT appear when filtered to warnings only
        Assert.DoesNotContain("DeadBlocks", result);
    }
}
