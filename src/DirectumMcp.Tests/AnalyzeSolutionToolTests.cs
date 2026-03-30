using DirectumMcp.DevTools.Tools;
using Xunit;

namespace DirectumMcp.Tests;

public class AnalyzeSolutionToolTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string? _previousSolutionPath;
    private readonly AnalyzeSolutionTool _tool;

    public AnalyzeSolutionToolTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "AnalyzeSolutionTests_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);

        _previousSolutionPath = Environment.GetEnvironmentVariable("SOLUTION_PATH");
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _tempDir);

        _tool = new AnalyzeSolutionTool();
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("SOLUTION_PATH", _previousSolutionPath);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private string CreateModuleDir(string relPath)
    {
        var dir = Path.Combine(_tempDir, relPath);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static void WriteFile(string dir, string relativePath, string content)
    {
        var fullPath = Path.Combine(dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private static string ModuleMtd(string name, string guid, string[]? deps = null, string[]? entityGuids = null)
    {
        var depsJson = deps == null ? "[]"
            : "[" + string.Join(",", deps.Select(d => $"{{\"Id\":\"{d}\"}}")) + "]";

        var entitiesJson = entityGuids == null ? "[]"
            : "[" + string.Join(",", entityGuids.Select(g => $"{{\"Id\":\"{g}\"}}")) + "]";

        return $$"""
            {
              "$type": "Sungero.Metadata.ModuleMetadata, Sungero.Metadata",
              "NameGuid": "{{guid}}",
              "Name": "{{name}}",
              "Dependencies": {{depsJson}},
              "Entities": {{entitiesJson}}
            }
            """;
    }

    private static string EntityMtd(string name, string guid, string? ancestorGuid = null, string? baseGuid = null, (string Name, string Code)[]? properties = null)
    {
        var ancestor = ancestorGuid != null ? $"\"AncestorGuid\": \"{ancestorGuid}\"," : "";
        var baseG = baseGuid ?? "00000000-0000-0000-0000-000000000000";

        var propsJson = properties == null ? "[]"
            : "[" + string.Join(",", properties.Select(p =>
                $"{{\"$type\":\"Sungero.Metadata.StringPropertyMetadata, Sungero.Metadata\",\"NameGuid\":\"{Guid.NewGuid()}\",\"Name\":\"{p.Name}\",\"Code\":\"{p.Code}\"}}")) + "]";

        return $$"""
            {
              "$type": "Sungero.Metadata.EntityMetadata, Sungero.Metadata",
              "NameGuid": "{{guid}}",
              "Name": "{{name}}",
              "BaseGuid": "{{baseG}}",
              {{ancestor}}
              "Properties": {{propsJson}}
            }
            """;
    }

    private static string PackageInfoXml(string name, string version) => $"""
        <?xml version="1.0" encoding="utf-8"?>
        <PackageInfo>
          <Name>{name}</Name>
          <Version>{version}</Version>
        </PackageInfo>
        """;

    // ─── 1. Health_EmptyDir_ReturnsNoModules ────────────────────────────────

    [Fact]
    public async Task Health_EmptyDir_ReturnsNoModules()
    {
        var result = await _tool.AnalyzeSolution(_tempDir, "health");

        Assert.Contains("0", result); // 0 modules
    }

    // ─── 2. Health_SingleModule_ShowsStats ──────────────────────────────────

    [Fact]
    public async Task Health_SingleModule_ShowsStats()
    {
        var modDir = CreateModuleDir("work/MyModule");
        var entityGuid = "aaaa0001-0000-0000-0000-000000000001";
        WriteFile(modDir, "Module.mtd", ModuleMtd("MyModule", "bbbb0001-0000-0000-0000-000000000001", entityGuids: [entityGuid]));
        WriteFile(modDir, "MyEntity.mtd", EntityMtd("MyEntity", entityGuid));

        var result = await _tool.AnalyzeSolution(_tempDir, "health");

        Assert.Contains("MyModule", result);
        Assert.Contains("1", result);
    }

    // ─── 3. Health_MultipleModules_ShowsAll ─────────────────────────────────

    [Fact]
    public async Task Health_MultipleModules_ShowsAll()
    {
        var modA = CreateModuleDir("work/ModuleA");
        var modB = CreateModuleDir("work/ModuleB");
        WriteFile(modA, "Module.mtd", ModuleMtd("ModuleA", "cccc0001-0000-0000-0000-000000000001"));
        WriteFile(modB, "Module.mtd", ModuleMtd("ModuleB", "cccc0002-0000-0000-0000-000000000002"));

        var result = await _tool.AnalyzeSolution(_tempDir, "health");

        Assert.Contains("ModuleA", result);
        Assert.Contains("ModuleB", result);
    }

    // ─── 4. Conflicts_NoOverrides_Clean ─────────────────────────────────────

    [Fact]
    public async Task Conflicts_NoOverrides_Clean()
    {
        var modDir = CreateModuleDir("work/ModuleClean");
        WriteFile(modDir, "Module.mtd", ModuleMtd("ModuleClean", "dddd0001-0000-0000-0000-000000000001"));
        WriteFile(modDir, "EntityA.mtd", EntityMtd("EntityA", "dddd0002-0000-0000-0000-000000000002"));
        WriteFile(modDir, "EntityB.mtd", EntityMtd("EntityB", "dddd0003-0000-0000-0000-000000000003"));

        var result = await _tool.AnalyzeSolution(_tempDir, "conflicts");

        Assert.Contains("не обнаружено", result);
    }

    // ─── 5. Conflicts_TwoModulesOverrideSameEntity_Detected ─────────────────

    [Fact]
    public async Task Conflicts_TwoModulesOverrideSameEntity_Detected()
    {
        var ancestorGuid = "eeee0000-0000-0000-0000-000000000000";
        var modA = CreateModuleDir("work/ModuleA");
        var modB = CreateModuleDir("work/ModuleB");

        WriteFile(modA, "Module.mtd", ModuleMtd("ModuleA", "eeee0001-0000-0000-0000-000000000001"));
        WriteFile(modB, "Module.mtd", ModuleMtd("ModuleB", "eeee0002-0000-0000-0000-000000000002"));
        WriteFile(modA, "OverrideA.mtd", EntityMtd("OverrideA", "eeee0003-0000-0000-0000-000000000003", ancestorGuid: ancestorGuid));
        WriteFile(modB, "OverrideB.mtd", EntityMtd("OverrideB", "eeee0004-0000-0000-0000-000000000004", ancestorGuid: ancestorGuid));

        var result = await _tool.AnalyzeSolution(_tempDir, "conflicts");

        Assert.Contains("OverrideA", result);
        Assert.Contains("OverrideB", result);
        Assert.Contains(ancestorGuid, result);
    }

    // ─── 6. Conflicts_DifferentAncestors_NoConflict ─────────────────────────

    [Fact]
    public async Task Conflicts_DifferentAncestors_NoConflict()
    {
        var ancestorA = "ffff0001-0000-0000-0000-000000000001";
        var ancestorB = "ffff0002-0000-0000-0000-000000000002";
        var modA = CreateModuleDir("work/ModDiffA");
        var modB = CreateModuleDir("work/ModDiffB");

        WriteFile(modA, "Module.mtd", ModuleMtd("ModDiffA", "ffff0003-0000-0000-0000-000000000003"));
        WriteFile(modB, "Module.mtd", ModuleMtd("ModDiffB", "ffff0004-0000-0000-0000-000000000004"));
        WriteFile(modA, "OverrideA.mtd", EntityMtd("OverrideA", "ffff0005-0000-0000-0000-000000000005", ancestorGuid: ancestorA));
        WriteFile(modB, "OverrideB.mtd", EntityMtd("OverrideB", "ffff0006-0000-0000-0000-000000000006", ancestorGuid: ancestorB));

        var result = await _tool.AnalyzeSolution(_tempDir, "conflicts");

        Assert.DoesNotContain("Обнаружено конфликтов", result);
    }

    // ─── 7. Orphans_UnreferencedCustomModule_Detected ───────────────────────

    [Fact]
    public async Task Orphans_UnreferencedCustomModule_Detected()
    {
        var modDir = CreateModuleDir("work/LonelyModule");
        WriteFile(modDir, "Module.mtd", ModuleMtd("LonelyModule", "abcd0001-0000-0000-0000-000000000001"));

        var result = await _tool.AnalyzeSolution(_tempDir, "orphans");

        Assert.Contains("LonelyModule", result);
    }

    // ─── 8. Orphans_ReferencedModule_NotOrphan ──────────────────────────────

    [Fact]
    public async Task Orphans_ReferencedModule_NotOrphan()
    {
        var depGuid = "abcd0002-0000-0000-0000-000000000002";
        var modA = CreateModuleDir("work/ModuleDependent");
        var modB = CreateModuleDir("work/ModuleBase");

        WriteFile(modA, "Module.mtd", ModuleMtd("ModuleDependent", "abcd0003-0000-0000-0000-000000000003", deps: [depGuid]));
        WriteFile(modB, "Module.mtd", ModuleMtd("ModuleBase", depGuid));

        var result = await _tool.AnalyzeSolution(_tempDir, "orphans");

        // ModuleBase is referenced so should NOT appear as orphan; ModuleDependent also depends on it
        // Neither should appear as orphan because ModuleDependent is referenced by no one but ModuleBase is depended on
        // Actually ModuleDependent is an orphan (no one depends on it), but ModuleBase is not
        Assert.DoesNotContain("ModuleBase", result.Split("Сиротские кастомные")[1].Split("##")[0]);
    }

    // ─── 9. Orphans_PlatformModule_NotOrphan ────────────────────────────────

    [Fact]
    public async Task Orphans_PlatformModule_NotOrphan()
    {
        // Platform modules (base/) should never be listed as orphans
        var baseDir = CreateModuleDir("base/PlatformModule");
        WriteFile(baseDir, "Module.mtd", ModuleMtd("PlatformModule", "abcd0010-0000-0000-0000-000000000010"));

        var result = await _tool.AnalyzeSolution(_tempDir, "orphans");

        // The orphans section should not include platform module
        var orphansSection = result.Contains("Сиротские кастомные")
            ? result.Split("Сиротские кастомные")[1].Split("##")[0]
            : result;
        Assert.DoesNotContain("PlatformModule", orphansSection);
    }

    // ─── 10. Duplicates_SameGuid_TwoEntities_Detected ───────────────────────

    [Fact]
    public async Task Duplicates_SameGuid_TwoEntities_Detected()
    {
        var sharedGuid = "dupe0001-0000-0000-0000-000000000001";
        var modA = CreateModuleDir("work/DupeModA");
        var modB = CreateModuleDir("work/DupeModB");

        WriteFile(modA, "Module.mtd", ModuleMtd("DupeModA", "dupe0002-0000-0000-0000-000000000002", entityGuids: [sharedGuid]));
        WriteFile(modB, "Module.mtd", ModuleMtd("DupeModB", "dupe0003-0000-0000-0000-000000000003", entityGuids: [sharedGuid]));
        WriteFile(modA, "EntityA.mtd", EntityMtd("EntityA", sharedGuid));
        WriteFile(modB, "EntityB.mtd", EntityMtd("EntityB", sharedGuid));

        var result = await _tool.AnalyzeSolution(_tempDir, "duplicates");

        Assert.Contains(sharedGuid, result);
        Assert.Contains("EntityA", result);
        Assert.Contains("EntityB", result);
    }

    // ─── 11. Duplicates_UniqueGuids_Clean ───────────────────────────────────

    [Fact]
    public async Task Duplicates_UniqueGuids_Clean()
    {
        var modDir = CreateModuleDir("work/UniqueGuidMod");
        WriteFile(modDir, "Module.mtd", ModuleMtd("UniqueGuidMod", "uniq0001-0000-0000-0000-000000000001",
            entityGuids: ["uniq0002-0000-0000-0000-000000000002", "uniq0003-0000-0000-0000-000000000003"]));
        WriteFile(modDir, "EntityA.mtd", EntityMtd("EntityA", "uniq0002-0000-0000-0000-000000000002"));
        WriteFile(modDir, "EntityB.mtd", EntityMtd("EntityB", "uniq0003-0000-0000-0000-000000000003"));

        var result = await _tool.AnalyzeSolution(_tempDir, "duplicates");

        Assert.Contains("не обнаружено", result);
    }

    // ─── 12. Duplicates_PropertyCodeCollision_Detected ──────────────────────

    [Fact]
    public async Task Duplicates_PropertyCodeCollision_Detected()
    {
        var parentGuid = "coll0001-0000-0000-0000-000000000001";
        var childGuid = "coll0002-0000-0000-0000-000000000002";
        var modDir = CreateModuleDir("work/CollisionMod");

        WriteFile(modDir, "Module.mtd", ModuleMtd("CollisionMod", "coll0003-0000-0000-0000-000000000003",
            entityGuids: [parentGuid, childGuid]));
        WriteFile(modDir, "ParentEntity.mtd", EntityMtd("ParentEntity", parentGuid,
            properties: [("DealNumber", "Deal")]));
        WriteFile(modDir, "ChildEntity.mtd", EntityMtd("ChildEntity", childGuid,
            baseGuid: parentGuid,
            properties: [("DealNumber", "Deal")])); // Same Code as parent

        var result = await _tool.AnalyzeSolution(_tempDir, "duplicates");

        Assert.Contains("Deal", result);
        Assert.Contains("ChildEntity", result);
        Assert.Contains("ParentEntity", result);
    }

    // ─── 13. Versions_MatchingVersions_Clean ────────────────────────────────

    [Fact]
    public async Task Versions_MatchingVersions_Clean()
    {
        var depGuid = "ver00001-0000-0000-0000-000000000001";
        var modA = CreateModuleDir("work/VerModA");
        var modB = CreateModuleDir("work/VerModB");

        WriteFile(modA, "Module.mtd", ModuleMtd("VerModA", "ver00002-0000-0000-0000-000000000002", deps: [depGuid]));
        WriteFile(modB, "Module.mtd", ModuleMtd("VerModB", depGuid));
        WriteFile(modA, "PackageInfo.xml", PackageInfoXml("VerModA", "1.0.0"));
        WriteFile(modB, "PackageInfo.xml", PackageInfoXml("VerModB", "1.0.0"));

        var result = await _tool.AnalyzeSolution(_tempDir, "versions");

        Assert.Contains("Несоответствий версий не обнаружено", result);
    }

    // ─── 14. Versions_MismatchedVersions_Flagged ────────────────────────────

    [Fact]
    public async Task Versions_MismatchedVersions_Flagged()
    {
        var depGuid = "ver10001-0000-0000-0000-000000000001";
        var modA = CreateModuleDir("work/VerMismatchA");
        var modB = CreateModuleDir("work/VerMismatchB");

        WriteFile(modA, "Module.mtd", ModuleMtd("VerMismatchA", "ver10002-0000-0000-0000-000000000002", deps: [depGuid]));
        WriteFile(modB, "Module.mtd", ModuleMtd("VerMismatchB", depGuid));
        WriteFile(modA, "PackageInfo.xml", PackageInfoXml("VerMismatchA", "1.0.0"));
        WriteFile(modB, "PackageInfo.xml", PackageInfoXml("VerMismatchB", "2.0.0"));

        var result = await _tool.AnalyzeSolution(_tempDir, "versions");

        Assert.Contains("Обнаружено несоответствий", result);
        Assert.Contains("VerMismatchA", result);
        Assert.Contains("VerMismatchB", result);
    }

    // ─── 15. PathDenied_ReturnsDenyMessage ──────────────────────────────────

    [Fact]
    public async Task PathDenied_ReturnsDenyMessage()
    {
        // Use a path that is definitely outside SOLUTION_PATH (which is _tempDir)
        var outsidePath = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(outsidePath) || !Directory.Exists(outsidePath))
            outsidePath = "C:\\Windows";
        if (!Directory.Exists(outsidePath))
        {
            // Skip on non-Windows
            return;
        }

        var result = await _tool.AnalyzeSolution(outsidePath, "health");

        Assert.Contains("ОШИБКА", result);
        Assert.Contains("запрещён", result);
    }
}
