using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using DirectumMcp.Core.Helpers;
using ModelContextProtocol.Server;

namespace DirectumMcp.DevTools.Tools;

[McpServerToolType]
public class ModifyWorkflowTool
{
    [McpServerTool(Name = "modify_workflow")]
    [Description("Изменить RouteScheme задачи: добавить/удалить блок, параллельная ветка.")]
    public async Task<string> ModifyWorkflow(
        [Description("Путь к .mtd файлу задачи")] string path,
        [Description("Действие: add_block | remove_block | add_parallel | reorder")] string action,
        [Description("Тип нового блока: Assignment | Notice | Condition | Script")] string? blockType = null,
        [Description("Имя нового блока")] string? blockName = null,
        [Description("GUID или имя блока, после которого вставить")] string? afterBlock = null,
        [Description("GUID или имя блока, перед которым вставить")] string? beforeBlock = null,
        [Description("GUID или имя блока для удаления/перемещения")] string? targetBlock = null,
        [Description("Предпросмотр без записи (по умолчанию: true)")] bool dryRun = true)
    {
        if (!PathGuard.IsAllowed(path))
            return PathGuard.DenyMessage(path);

        if (!File.Exists(path))
            return $"**ОШИБКА**: Файл не найден: `{path}`";

        if (!path.EndsWith(".mtd", StringComparison.OrdinalIgnoreCase))
            return $"**ОШИБКА**: Файл должен иметь расширение .mtd: `{path}`";

        string json;
        try
        {
            json = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            return $"**ОШИБКА**: Не удалось прочитать файл: {ex.Message}";
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(json, new JsonNodeOptions { PropertyNameCaseInsensitive = false });
        }
        catch (JsonException ex)
        {
            return $"**ОШИБКА**: Ошибка парсинга JSON: {ex.Message}";
        }

        if (root == null)
            return "**ОШИБКА**: Не удалось распарсить файл как JSON.";

        var blocks = GetBlocksArray(root);
        if (blocks == null)
            return "**ОШИБКА**: В файле не найден RouteScheme.Blocks. Убедитесь, что это .mtd файл задачи.";

        var actionLower = action.ToLowerInvariant();
        return actionLower switch
        {
            "add_block"     => await ExecuteAddBlock(root, blocks, path, blockType, blockName, afterBlock, dryRun),
            "remove_block"  => await ExecuteRemoveBlock(root, blocks, path, targetBlock, dryRun),
            "add_parallel"  => await ExecuteAddParallel(root, blocks, path, blockName, afterBlock, beforeBlock, blockType, dryRun),
            "reorder"       => await ExecuteReorder(root, blocks, path, targetBlock, afterBlock, dryRun),
            _               => $"**ОШИБКА**: Неизвестное действие `{action}`. Допустимые значения: add_block, remove_block, add_parallel, reorder."
        };
    }

    // ─── add_block ────────────────────────────────────────────────────────────

    private static async Task<string> ExecuteAddBlock(
        JsonNode root, JsonArray blocks, string path,
        string? blockType, string? blockName, string? afterBlock,
        bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(blockType))
            return "**ОШИБКА**: Параметр `blockType` обязателен для действия add_block. Допустимые значения: Assignment, Notice, Condition, Script.";

        if (string.IsNullOrWhiteSpace(blockName))
            return "**ОШИБКА**: Параметр `blockName` обязателен для действия add_block.";

        if (string.IsNullOrWhiteSpace(afterBlock))
            return "**ОШИБКА**: Параметр `afterBlock` обязателен для действия add_block.";

        var afterNode = FindBlock(blocks, afterBlock);
        if (afterNode == null)
            return $"**ОШИБКА**: Блок `{afterBlock}` не найден в RouteScheme.Blocks.";

        var (typeStr, blockTypeStr) = ResolveBlockTypeStrings(blockType);
        if (typeStr == null)
            return $"**ОШИБКА**: Неизвестный тип блока `{blockType}`. Допустимые значения: Assignment, Notice, Condition, Script.";

        var newGuid = Guid.NewGuid().ToString();

        // Collect outgoing connectors of afterBlock before rewiring
        var afterConnectorsBefore = GetConnectorsList(afterNode);

        // Create the new block with the original targets of afterBlock
        var newBlock = CreateBlock(typeStr, blockTypeStr!, newGuid, blockName, afterConnectorsBefore);

        // Rewire afterBlock: all its connectors now point only to the new block
        SetSingleConnector(afterNode, newGuid);

        // Insert new block into blocks array (right after afterBlock)
        var afterIndex = IndexOf(blocks, afterNode);
        blocks.Insert(afterIndex + 1, newBlock);

        var report = BuildReport(
            dryRun,
            "add_block",
            path,
            blocks,
            $"Создан новый блок `{blockName}` (GUID: `{newGuid}`, тип: `{blockTypeStr}`) после блока `{GetName(afterNode)}`.\n" +
            $"Перенаправлены переходы: `{GetName(afterNode)}` → `{blockName}` → {string.Join(", ", afterConnectorsBefore.Select(c => $"`{c}`"))}");

        if (!dryRun)
            await WriteJson(path, root);

        return report;
    }

    // ─── remove_block ─────────────────────────────────────────────────────────

    private static async Task<string> ExecuteRemoveBlock(
        JsonNode root, JsonArray blocks, string path,
        string? targetBlock, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(targetBlock))
            return "**ОШИБКА**: Параметр `targetBlock` обязателен для действия remove_block.";

        var target = FindBlock(blocks, targetBlock);
        if (target == null)
            return $"**ОШИБКА**: Блок `{targetBlock}` не найден в RouteScheme.Blocks.";

        var targetBlockType = target["BlockType"]?.GetValue<string>() ?? "";
        if (string.Equals(targetBlockType, "StartBlock", StringComparison.OrdinalIgnoreCase))
            return "**ОШИБКА**: Нельзя удалить StartBlock.";
        if (string.Equals(targetBlockType, "EndBlock", StringComparison.OrdinalIgnoreCase))
            return "**ОШИБКА**: Нельзя удалить EndBlock.";

        var targetGuid = target["NameGuid"]?.GetValue<string>() ?? "";
        var targetName = GetName(target);
        var outgoing = GetConnectorsList(target);

        // Find all blocks that point to targetBlock
        var incoming = FindIncomingBlocks(blocks, targetGuid);

        // Rewire: incoming blocks now point to the first outgoing target (if any)
        var firstOutgoing = outgoing.FirstOrDefault();
        foreach (var inBlock in incoming)
        {
            RewireConnector(inBlock, targetGuid, firstOutgoing ?? "");
        }

        // Remove from array
        var idx = IndexOf(blocks, target);
        blocks.RemoveAt(idx);

        var report = BuildReport(
            dryRun,
            "remove_block",
            path,
            blocks,
            $"Блок `{targetName}` (GUID: `{targetGuid}`) удалён.\n" +
            $"Входящие блоки: {string.Join(", ", incoming.Select(b => $"`{GetName(b)}`"))}.\n" +
            $"Переходы перенаправлены на: `{firstOutgoing ?? "(нет)"}`.`");

        if (!dryRun)
            await WriteJson(path, root);

        return report;
    }

    // ─── add_parallel ─────────────────────────────────────────────────────────

    private static async Task<string> ExecuteAddParallel(
        JsonNode root, JsonArray blocks, string path,
        string? blockName, string? afterBlock, string? beforeBlock,
        string? blockType, bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(blockName))
            return "**ОШИБКА**: Параметр `blockName` обязателен для действия add_parallel.";

        if (string.IsNullOrWhiteSpace(afterBlock))
            return "**ОШИБКА**: Параметр `afterBlock` обязателен для действия add_parallel.";

        var afterNode = FindBlock(blocks, afterBlock);
        if (afterNode == null)
            return $"**ОШИБКА**: Блок `{afterBlock}` не найден в RouteScheme.Blocks.";

        var (typeStr, blockTypeStr) = ResolveBlockTypeStrings(blockType ?? "Assignment");
        if (typeStr == null)
            return $"**ОШИБКА**: Неизвестный тип блока `{blockType}`. Допустимые значения: Assignment, Notice, Condition, Script.";

        // Determine the join point
        string? joinGuid = null;
        if (!string.IsNullOrWhiteSpace(beforeBlock))
        {
            var joinNode = FindBlock(blocks, beforeBlock);
            if (joinNode == null)
                return $"**ОШИБКА**: Блок `{beforeBlock}` (beforeBlock) не найден в RouteScheme.Blocks.";
            joinGuid = joinNode["NameGuid"]?.GetValue<string>();
        }
        else
        {
            // Use original target of afterBlock as join point
            var afterConnectors = GetConnectorsList(afterNode);
            joinGuid = afterConnectors.FirstOrDefault();
        }

        var guid1 = Guid.NewGuid().ToString();
        var guid2 = Guid.NewGuid().ToString();
        var name1 = blockName + "_1";
        var name2 = blockName + "_2";

        var targets1 = joinGuid != null ? new List<string> { joinGuid } : new List<string>();
        var targets2 = joinGuid != null ? new List<string> { joinGuid } : new List<string>();

        var newBlock1 = CreateBlock(typeStr, blockTypeStr!, guid1, name1, targets1);
        var newBlock2 = CreateBlock(typeStr, blockTypeStr!, guid2, name2, targets2);

        // afterBlock now connects to BOTH new blocks
        SetDoubleConnector(afterNode, guid1, guid2);

        var afterIndex = IndexOf(blocks, afterNode);
        blocks.Insert(afterIndex + 1, newBlock2);
        blocks.Insert(afterIndex + 1, newBlock1);

        var report = BuildReport(
            dryRun,
            "add_parallel",
            path,
            blocks,
            $"Создана параллельная ветка после блока `{GetName(afterNode)}`:\n" +
            $"- `{name1}` (GUID: `{guid1}`)\n" +
            $"- `{name2}` (GUID: `{guid2}`)\n" +
            $"Оба блока ведут к: `{joinGuid ?? "(нет)"}`.");

        if (!dryRun)
            await WriteJson(path, root);

        return report;
    }

    // ─── reorder ──────────────────────────────────────────────────────────────

    private static async Task<string> ExecuteReorder(
        JsonNode root, JsonArray blocks, string path,
        string? targetBlock, string? afterBlock,
        bool dryRun)
    {
        if (string.IsNullOrWhiteSpace(targetBlock))
            return "**ОШИБКА**: Параметр `targetBlock` обязателен для действия reorder.";

        if (string.IsNullOrWhiteSpace(afterBlock))
            return "**ОШИБКА**: Параметр `afterBlock` обязателен для действия reorder.";

        var target = FindBlock(blocks, targetBlock);
        if (target == null)
            return $"**ОШИБКА**: Блок `{targetBlock}` не найден в RouteScheme.Blocks.";

        var afterNode = FindBlock(blocks, afterBlock);
        if (afterNode == null)
            return $"**ОШИБКА**: Блок `{afterBlock}` не найден в RouteScheme.Blocks.";

        var targetGuid = target["NameGuid"]?.GetValue<string>() ?? "";
        var targetName = GetName(target);

        if (string.Equals(targetGuid, afterNode["NameGuid"]?.GetValue<string>(), StringComparison.OrdinalIgnoreCase))
            return "**ОШИБКА**: `targetBlock` и `afterBlock` не могут быть одним и тем же блоком.";

        // Step 1: Remove target from current position, reconnecting neighbors
        var outgoing = GetConnectorsList(target);
        var firstOutgoing = outgoing.FirstOrDefault();
        var incoming = FindIncomingBlocks(blocks, targetGuid);
        foreach (var inBlock in incoming)
            RewireConnector(inBlock, targetGuid, firstOutgoing ?? "");

        var oldIndex = IndexOf(blocks, target);
        blocks.RemoveAt(oldIndex);

        // Step 2: Recalculate afterNode index (may have shifted)
        var afterNodeRefreshed = FindBlock(blocks, afterBlock)!;
        var afterIndex = IndexOf(blocks, afterNodeRefreshed);

        // Step 3: Rewire afterNode → target → afterNode's old targets
        var afterOutgoing = GetConnectorsList(afterNodeRefreshed);
        SetConnectors(target, afterOutgoing);
        SetSingleConnector(afterNodeRefreshed, targetGuid);

        // Step 4: Insert target after afterNode
        blocks.Insert(afterIndex + 1, target);

        var report = BuildReport(
            dryRun,
            "reorder",
            path,
            blocks,
            $"Блок `{targetName}` перемещён после блока `{GetName(afterNodeRefreshed)}`.\n" +
            $"Переходы обновлены: `{GetName(afterNodeRefreshed)}` → `{targetName}` → {string.Join(", ", afterOutgoing.Select(g => $"`{g}`"))}.");

        if (!dryRun)
            await WriteJson(path, root);

        return report;
    }

    // ─── JSON helpers ─────────────────────────────────────────────────────────

    private static JsonArray? GetBlocksArray(JsonNode root)
    {
        var routeScheme = root["RouteScheme"];
        if (routeScheme != null)
            return routeScheme["Blocks"] as JsonArray;

        return root["Blocks"] as JsonArray;
    }

    internal static JsonNode? FindBlock(JsonArray blocks, string guidOrName)
    {
        foreach (var block in blocks)
        {
            if (block == null) continue;
            var guid = block["NameGuid"]?.GetValue<string>();
            var name = block["Name"]?.GetValue<string>();
            if (string.Equals(guid, guidOrName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, guidOrName, StringComparison.OrdinalIgnoreCase))
                return block;
        }
        return null;
    }

    private static List<string> GetConnectorsList(JsonNode block)
    {
        var connectors = block["Connectors"] as JsonArray;
        if (connectors == null) return new List<string>();
        var result = new List<string>();
        foreach (var c in connectors)
        {
            if (c == null) continue;
            var to = c["ToBlock"]?.GetValue<string>();
            if (!string.IsNullOrEmpty(to))
                result.Add(to);
        }
        return result;
    }

    private static void SetSingleConnector(JsonNode block, string toGuid)
    {
        var arr = new JsonArray();
        var conn = new JsonObject
        {
            ["ToBlock"] = toGuid,
            ["Condition"] = ""
        };
        arr.Add(conn);
        block["Connectors"] = arr;
    }

    private static void SetDoubleConnector(JsonNode block, string toGuid1, string toGuid2)
    {
        var arr = new JsonArray();
        arr.Add(new JsonObject { ["ToBlock"] = toGuid1, ["Condition"] = "" });
        arr.Add(new JsonObject { ["ToBlock"] = toGuid2, ["Condition"] = "" });
        block["Connectors"] = arr;
    }

    private static void SetConnectors(JsonNode block, List<string> targets)
    {
        var arr = new JsonArray();
        foreach (var t in targets)
            arr.Add(new JsonObject { ["ToBlock"] = t, ["Condition"] = "" });
        block["Connectors"] = arr;
    }

    private static void RewireConnector(JsonNode block, string oldTarget, string newTarget)
    {
        var connectors = block["Connectors"] as JsonArray;
        if (connectors == null) return;
        foreach (var c in connectors)
        {
            if (c == null) continue;
            var to = c["ToBlock"]?.GetValue<string>();
            if (string.Equals(to, oldTarget, StringComparison.OrdinalIgnoreCase))
                c["ToBlock"] = newTarget;
        }
    }

    private static List<JsonNode> FindIncomingBlocks(JsonArray blocks, string targetGuid)
    {
        var result = new List<JsonNode>();
        foreach (var block in blocks)
        {
            if (block == null) continue;
            var connectors = block["Connectors"] as JsonArray;
            if (connectors == null) continue;
            foreach (var c in connectors)
            {
                if (c == null) continue;
                var to = c["ToBlock"]?.GetValue<string>();
                if (string.Equals(to, targetGuid, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(block);
                    break;
                }
            }
        }
        return result;
    }

    private static int IndexOf(JsonArray blocks, JsonNode node)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (ReferenceEquals(blocks[i], node)) return i;
        }
        return -1;
    }

    private static string GetName(JsonNode block) =>
        block["Name"]?.GetValue<string>() ?? block["NameGuid"]?.GetValue<string>() ?? "(unknown)";

    private static JsonNode CreateBlock(
        string typeStr, string blockTypeStr, string guid, string name, List<string> targets)
    {
        var connectors = new JsonArray();
        foreach (var t in targets)
            connectors.Add(new JsonObject { ["ToBlock"] = t, ["Condition"] = "" });

        var block = new JsonObject
        {
            ["$type"] = typeStr,
            ["NameGuid"] = guid,
            ["Name"] = name,
            ["BlockType"] = blockTypeStr,
            ["GenerateHandler"] = true,
            ["Connectors"] = connectors
        };
        return block;
    }

    private static (string? typeStr, string? blockTypeStr) ResolveBlockTypeStrings(string blockType)
    {
        return blockType.ToLowerInvariant() switch
        {
            "assignment" => (
                "Sungero.Metadata.AssignmentBlockMetadata, Sungero.Metadata",
                "AssignmentBlock"),
            "notice" => (
                "Sungero.Metadata.NoticeBlockMetadata, Sungero.Metadata",
                "NoticeBlock"),
            "condition" => (
                "Sungero.Metadata.ConditionBlockMetadata, Sungero.Metadata",
                "ConditionBlock"),
            "script" => (
                "Sungero.Metadata.ScriptBlockMetadata, Sungero.Metadata",
                "ScriptBlock"),
            _ => (null, null)
        };
    }

    private static async Task WriteJson(string path, JsonNode root)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var output = root.ToJsonString(options);
        await File.WriteAllTextAsync(path, output, Encoding.UTF8);
    }

    // ─── Report formatting ────────────────────────────────────────────────────

    private static string BuildReport(
        bool dryRun,
        string action,
        string path,
        JsonArray blocks,
        string details)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Модификация маршрутной схемы Directum RX");
        sb.AppendLine();
        sb.AppendLine($"**Файл**: `{path}`");
        sb.AppendLine($"**Действие**: `{action}`");
        sb.AppendLine($"**Режим**: {(dryRun ? "предпросмотр (dryRun=true, файл не изменён)" : "запись (dryRun=false, файл изменён)")}");
        sb.AppendLine();
        sb.AppendLine("## Результат");
        sb.AppendLine();
        sb.AppendLine(details);
        sb.AppendLine();
        sb.AppendLine("## Итоговая схема блоков");
        sb.AppendLine();
        sb.AppendLine("| # | Имя | Тип | Переходы |");
        sb.AppendLine("|---|-----|-----|---------|");
        for (int i = 0; i < blocks.Count; i++)
        {
            var b = blocks[i];
            if (b == null) continue;
            var bName = b["Name"]?.GetValue<string>() ?? "(?)";
            var bType = b["BlockType"]?.GetValue<string>() ?? "(?)";
            var targets = GetConnectorsList(b);
            var targetStr = targets.Count > 0 ? string.Join(", ", targets.Select(t => $"`{t}`")) : "—";
            sb.AppendLine($"| {i + 1} | `{bName}` | `{bType}` | {targetStr} |");
        }
        sb.AppendLine();
        if (dryRun)
            sb.AppendLine("> **Предпросмотр**: изменения показаны выше, но не записаны в файл. Передайте `dryRun=false` для применения.");

        return sb.ToString();
    }
}
