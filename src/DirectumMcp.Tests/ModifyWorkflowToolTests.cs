using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class ModifyWorkflowToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly ModifyWorkflowTool _tool;

    // Shared fixture JSON: StartBlock → ReviewTask → EndBlock
    private const string BaseRouteJson = """
        {
          "$type": "Sungero.Metadata.TaskMetadata, Sungero.Metadata",
          "NameGuid": "task-0000-0000-0000-000000000001",
          "Name": "TestTask",
          "RouteScheme": {
            "Blocks": [
              {
                "$type": "Sungero.Metadata.StartBlockMetadata, Sungero.Metadata",
                "NameGuid": "start-guid",
                "Name": "StartBlock",
                "BlockType": "StartBlock",
                "Connectors": [{ "ToBlock": "task-guid", "Condition": "" }]
              },
              {
                "$type": "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Metadata",
                "NameGuid": "task-guid",
                "Name": "ReviewTask",
                "BlockType": "AssignmentBlock",
                "GenerateHandler": true,
                "Connectors": [{ "ToBlock": "end-guid", "Condition": "" }]
              },
              {
                "$type": "Sungero.Metadata.EndBlockMetadata, Sungero.Metadata",
                "NameGuid": "end-guid",
                "Name": "EndBlock",
                "BlockType": "EndBlock",
                "Connectors": []
              }
            ]
          }
        }
        """;

    public ModifyWorkflowToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "MwfTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);
        _tool = new ModifyWorkflowTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private string WriteMtd(string fileName, string json)
    {
        var path = Path.Combine(_tempDir, fileName);
        File.WriteAllText(path, json);
        return path;
    }

    private string WriteBaseMtd(string fileName = "Task.mtd") =>
        WriteMtd(fileName, BaseRouteJson);

    private static JsonArray GetBlocks(string json)
    {
        var root = JsonNode.Parse(json)!;
        return (root["RouteScheme"]!["Blocks"] as JsonArray)!;
    }

    private static JsonNode? BlockByName(JsonArray blocks, string name) =>
        blocks.FirstOrDefault(b => b?["Name"]?.GetValue<string>() == name);

    private static List<string> Targets(JsonNode block)
    {
        var arr = block["Connectors"] as JsonArray;
        if (arr == null) return new();
        return arr
            .Where(c => c != null)
            .Select(c => c!["ToBlock"]?.GetValue<string>() ?? "")
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
    }

    // ─── Test 1: AddBlock_AfterStart_InsertsCorrectly ─────────────────────────

    [Fact]
    public async Task AddBlock_AfterStart_InsertsCorrectly()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "add_block",
            blockType: "Assignment",
            blockName: "ApproveBlock",
            afterBlock: "StartBlock",
            dryRun: true);

        Assert.Contains("ApproveBlock", result);
        Assert.Contains("add_block", result);
        // File should NOT be written in dryRun mode
        var diskContent = await File.ReadAllTextAsync(path);
        Assert.DoesNotContain("ApproveBlock", diskContent);
    }

    // ─── Test 2: AddBlock_BetweenTwoBlocks_RewiredCorrectly ──────────────────

    [Fact]
    public async Task AddBlock_BetweenTwoBlocks_RewiredCorrectly()
    {
        var path = WriteBaseMtd();

        // Insert "SignBlock" after "ReviewTask" (which currently points to "end-guid")
        await _tool.ModifyWorkflow(
            path, "add_block",
            blockType: "Assignment",
            blockName: "SignBlock",
            afterBlock: "ReviewTask",
            dryRun: false);

        var saved = await File.ReadAllTextAsync(path);
        var blocks = GetBlocks(saved);

        var reviewBlock = BlockByName(blocks, "ReviewTask")!;
        var signBlock   = BlockByName(blocks, "SignBlock")!;

        Assert.NotNull(signBlock);
        // ReviewTask now points to SignBlock
        Assert.Contains("SignBlock", Targets(reviewBlock)
            .Select(g => blocks.FirstOrDefault(b => b?["NameGuid"]?.GetValue<string>() == g)?["Name"]?.GetValue<string>() ?? g));
        // SignBlock points to EndBlock
        Assert.Contains("end-guid", Targets(signBlock));
    }

    // ─── Test 3: AddBlock_MissingAfterBlock_ReturnsError ─────────────────────

    [Fact]
    public async Task AddBlock_MissingAfterBlock_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "add_block",
            blockType: "Assignment",
            blockName: "NewBlock",
            afterBlock: "NonExistentBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("NonExistentBlock", result);
    }

    // ─── Test 4: AddBlock_MissingBlockType_ReturnsError ──────────────────────

    [Fact]
    public async Task AddBlock_MissingBlockType_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "add_block",
            blockName: "NewBlock",
            afterBlock: "StartBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("blockType", result);
    }

    // ─── Test 5: AddBlock_DryRun_DoesNotModifyFile ───────────────────────────

    [Fact]
    public async Task AddBlock_DryRun_DoesNotModifyFile()
    {
        var path = WriteBaseMtd();
        var before = await File.ReadAllTextAsync(path);

        await _tool.ModifyWorkflow(
            path, "add_block",
            blockType: "Notice",
            blockName: "NotifyBlock",
            afterBlock: "StartBlock",
            dryRun: true);

        var after = await File.ReadAllTextAsync(path);
        Assert.Equal(before, after);
    }

    // ─── Test 6: AddBlock_DryRunFalse_WritesFile ─────────────────────────────

    [Fact]
    public async Task AddBlock_DryRunFalse_WritesFile()
    {
        var path = WriteBaseMtd();

        await _tool.ModifyWorkflow(
            path, "add_block",
            blockType: "Script",
            blockName: "ScriptBlock",
            afterBlock: "StartBlock",
            dryRun: false);

        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("ScriptBlock", saved);
    }

    // ─── Test 7: RemoveBlock_Middle_ReconnectsNeighbors ──────────────────────

    [Fact]
    public async Task RemoveBlock_Middle_ReconnectsNeighbors()
    {
        var path = WriteBaseMtd();

        // Remove the middle block "ReviewTask" (start → ReviewTask → end)
        // After removal: start should point to end
        await _tool.ModifyWorkflow(
            path, "remove_block",
            targetBlock: "ReviewTask",
            dryRun: false);

        var saved = await File.ReadAllTextAsync(path);
        var blocks = GetBlocks(saved);

        // ReviewTask should be gone
        Assert.Null(BlockByName(blocks, "ReviewTask"));

        // StartBlock should now point to end-guid
        var startBlock = BlockByName(blocks, "StartBlock")!;
        Assert.Contains("end-guid", Targets(startBlock));
    }

    // ─── Test 8: RemoveBlock_NotFound_ReturnsError ────────────────────────────

    [Fact]
    public async Task RemoveBlock_NotFound_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "remove_block",
            targetBlock: "GhostBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("GhostBlock", result);
    }

    // ─── Test 9: RemoveBlock_StartBlock_ReturnsError ──────────────────────────

    [Fact]
    public async Task RemoveBlock_StartBlock_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "remove_block",
            targetBlock: "StartBlock",
            dryRun: false);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("StartBlock", result);
        // File should be unchanged
        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("StartBlock", saved);
    }

    // ─── Test 10: RemoveBlock_EndBlock_ReturnsError ───────────────────────────

    [Fact]
    public async Task RemoveBlock_EndBlock_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "remove_block",
            targetBlock: "EndBlock",
            dryRun: false);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("EndBlock", result);
        // File should be unchanged
        var saved = await File.ReadAllTextAsync(path);
        Assert.Contains("EndBlock", saved);
    }

    // ─── Test 11: AddParallel_CreatesTwoBranches ──────────────────────────────

    [Fact]
    public async Task AddParallel_CreatesTwoBranches()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "add_parallel",
            blockName: "ParallelReview",
            afterBlock: "StartBlock",
            dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);

        var saved = await File.ReadAllTextAsync(path);
        var blocks = GetBlocks(saved);

        var block1 = BlockByName(blocks, "ParallelReview_1");
        var block2 = BlockByName(blocks, "ParallelReview_2");
        Assert.NotNull(block1);
        Assert.NotNull(block2);

        // StartBlock should now point to BOTH new blocks
        var startBlock = BlockByName(blocks, "StartBlock")!;
        var startTargets = Targets(startBlock);
        Assert.Equal(2, startTargets.Count);

        // Both branches should eventually point to the same join point (task-guid, the original target)
        Assert.Contains("task-guid", Targets(block1!));
        Assert.Contains("task-guid", Targets(block2!));
    }

    // ─── Test 12: AddParallel_MissingParams_ReturnsError ─────────────────────

    [Fact]
    public async Task AddParallel_MissingParams_ReturnsError()
    {
        var path = WriteBaseMtd();

        // Missing blockName
        var result1 = await _tool.ModifyWorkflow(
            path, "add_parallel",
            afterBlock: "StartBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result1);
        Assert.Contains("blockName", result1);

        // Missing afterBlock
        var result2 = await _tool.ModifyWorkflow(
            path, "add_parallel",
            blockName: "ParallelBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result2);
        Assert.Contains("afterBlock", result2);
    }

    // ─── Test 13: Reorder_MoveBlockAfterAnother ───────────────────────────────

    [Fact]
    public async Task Reorder_MoveBlockAfterAnother()
    {
        // Build a 4-block chain: Start → A → B → End
        // Reorder B to appear before A: Start → B → A → End
        var fourBlockJson = """
            {
              "RouteScheme": {
                "Blocks": [
                  {
                    "$type": "Sungero.Metadata.StartBlockMetadata, Sungero.Metadata",
                    "NameGuid": "start-guid",
                    "Name": "StartBlock",
                    "BlockType": "StartBlock",
                    "Connectors": [{ "ToBlock": "a-guid", "Condition": "" }]
                  },
                  {
                    "$type": "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Metadata",
                    "NameGuid": "a-guid",
                    "Name": "BlockA",
                    "BlockType": "AssignmentBlock",
                    "GenerateHandler": true,
                    "Connectors": [{ "ToBlock": "b-guid", "Condition": "" }]
                  },
                  {
                    "$type": "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Metadata",
                    "NameGuid": "b-guid",
                    "Name": "BlockB",
                    "BlockType": "AssignmentBlock",
                    "GenerateHandler": true,
                    "Connectors": [{ "ToBlock": "end-guid", "Condition": "" }]
                  },
                  {
                    "$type": "Sungero.Metadata.EndBlockMetadata, Sungero.Metadata",
                    "NameGuid": "end-guid",
                    "Name": "EndBlock",
                    "BlockType": "EndBlock",
                    "Connectors": []
                  }
                ]
              }
            }
            """;

        var path = WriteMtd("FourBlock.mtd", fourBlockJson);

        // Move BlockB to appear after StartBlock (before BlockA)
        var result = await _tool.ModifyWorkflow(
            path, "reorder",
            targetBlock: "BlockB",
            afterBlock: "StartBlock",
            dryRun: false);

        Assert.DoesNotContain("ОШИБКА", result);

        var saved = await File.ReadAllTextAsync(path);
        var blocks = GetBlocks(saved);

        // New order should be: Start → BlockB → BlockA → End
        var startBlock = BlockByName(blocks, "StartBlock")!;
        var blockB     = BlockByName(blocks, "BlockB")!;
        var blockA     = BlockByName(blocks, "BlockA")!;

        // StartBlock should now point to BlockB
        Assert.Contains("b-guid", Targets(startBlock));
        // BlockB should now point to BlockA
        Assert.Contains("a-guid", Targets(blockB));
        // BlockA should now point to EndBlock
        Assert.Contains("end-guid", Targets(blockA));
    }

    // ─── Test 14: Reorder_MissingTarget_ReturnsError ─────────────────────────

    [Fact]
    public async Task Reorder_MissingTarget_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "reorder",
            targetBlock: "NonExistentBlock",
            afterBlock: "StartBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("NonExistentBlock", result);
    }

    // ─── Test 15: PathDenied_ReturnsDenyMessage ───────────────────────────────

    [Fact]
    public async Task PathDenied_ReturnsDenyMessage()
    {
        // A path clearly outside tempDir and SOLUTION_PATH
        var outsidePath = Path.Combine(
            Path.GetPathRoot(Path.GetTempPath()) ?? "C:\\",
            "windows", "system32", "some_task.mtd");

        var result = await _tool.ModifyWorkflow(
            outsidePath, "add_block",
            blockType: "Assignment",
            blockName: "X",
            afterBlock: "StartBlock",
            dryRun: true);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }

    // ─── Additional: FindBlock helper tests ──────────────────────────────────

    [Fact]
    public void FindBlock_ByGuid_ReturnsBlock()
    {
        var blocks = GetBlocks(BaseRouteJson);
        var found = ModifyWorkflowTool.FindBlock(blocks, "start-guid");
        Assert.NotNull(found);
        Assert.Equal("StartBlock", found!["Name"]?.GetValue<string>());
    }

    [Fact]
    public void FindBlock_ByName_ReturnsBlock()
    {
        var blocks = GetBlocks(BaseRouteJson);
        var found = ModifyWorkflowTool.FindBlock(blocks, "ReviewTask");
        Assert.NotNull(found);
        Assert.Equal("task-guid", found!["NameGuid"]?.GetValue<string>());
    }

    [Fact]
    public void FindBlock_NotFound_ReturnsNull()
    {
        var blocks = GetBlocks(BaseRouteJson);
        var found = ModifyWorkflowTool.FindBlock(blocks, "DoesNotExist");
        Assert.Null(found);
    }

    [Fact]
    public async Task AddBlock_AllBlockTypes_CreatedCorrectly()
    {
        foreach (var (bt, expectedType) in new[]
        {
            ("Assignment", "AssignmentBlock"),
            ("Notice",     "NoticeBlock"),
            ("Condition",  "ConditionBlock"),
            ("Script",     "ScriptBlock"),
        })
        {
            var path = WriteBaseMtd($"Task_{bt}.mtd");

            await _tool.ModifyWorkflow(
                path, "add_block",
                blockType: bt,
                blockName: $"New{bt}",
                afterBlock: "StartBlock",
                dryRun: false);

            var saved = await File.ReadAllTextAsync(path);
            Assert.Contains(expectedType, saved);
            Assert.Contains($"New{bt}", saved);
        }
    }

    [Fact]
    public async Task UnknownAction_ReturnsError()
    {
        var path = WriteBaseMtd();

        var result = await _tool.ModifyWorkflow(
            path, "teleport",
            dryRun: true);

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("teleport", result);
    }
}
