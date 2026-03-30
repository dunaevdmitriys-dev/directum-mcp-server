using Xunit;

namespace DirectumMcp.Tests;

public class ScaffoldDialogToolTests
{
    private readonly DirectumMcp.DevTools.Tools.ScaffoldDialogTool _tool = new();

    [Fact]
    public async Task ScaffoldDialog_GeneratesCode()
    {
        var result = await _tool.ScaffoldDialog("CreateDeal", "DirRX.CRM",
            fields: "Name:string:required,Amount:double,Date:date",
            title: "Создать сделку");

        Assert.Contains("Диалог создан", result);
        Assert.Contains("CreateDeal", result);
        Assert.Contains("Создать сделку", result);
        Assert.Contains("dialog.AddString", result);
        Assert.Contains("dialog.AddDouble", result);
        Assert.Contains("dialog.AddDate", result);
    }

    [Fact]
    public async Task ScaffoldDialog_RequiredFields()
    {
        var result = await _tool.ScaffoldDialog("Test", "Mod",
            fields: "Name:string:required");

        Assert.Contains("true", result); // required = true
    }

    [Fact]
    public async Task ScaffoldDialog_NavigationField()
    {
        var result = await _tool.ScaffoldDialog("Assign", "Mod",
            fields: "Employee:navigation:Employee");

        Assert.Contains("dialog.AddSelect", result);
    }

    [Fact]
    public async Task ScaffoldDialog_BoolField()
    {
        var result = await _tool.ScaffoldDialog("Filter", "Mod",
            fields: "ShowAll:bool");

        Assert.Contains("dialog.AddBoolean", result);
    }

    [Fact]
    public async Task ScaffoldDialog_CascadeDependency()
    {
        var result = await _tool.ScaffoldDialog("SelectEmployee", "Mod",
            fields: "Department:navigation:Department,Employee:navigation:Employee",
            cascades: "Department→Employee");

        Assert.Contains("SetOnRefresh", result);
        Assert.Contains("Фильтрация", result);
    }

    [Fact]
    public async Task ScaffoldDialog_LocalizeFunction()
    {
        var result = await _tool.ScaffoldDialog("MyDialog", "DirRX.Module");

        Assert.Contains("LocalizeFunction", result);
        Assert.Contains("MyDialogFunctionName", result);
    }

    [Fact]
    public async Task ScaffoldDialog_ShowsUsageInstructions()
    {
        var result = await _tool.ScaffoldDialog("Test", "Mod");

        Assert.Contains("ModuleClientFunctions.cs", result);
        Assert.Contains("CoverFunctionAction", result);
    }

    [Fact]
    public async Task ScaffoldDialog_MultilineField()
    {
        var result = await _tool.ScaffoldDialog("Note", "Mod",
            fields: "Comment:text");

        Assert.Contains("AddMultilineString", result);
    }

    [Fact]
    public async Task ScaffoldDialog_IntegerField()
    {
        var result = await _tool.ScaffoldDialog("Count", "Mod",
            fields: "Quantity:int:required");

        Assert.Contains("AddInteger", result);
    }

    [Fact]
    public async Task ScaffoldDialog_EmptyFields_StillGenerates()
    {
        var result = await _tool.ScaffoldDialog("Empty", "Mod");

        Assert.Contains("Диалог создан", result);
        Assert.Contains("CreateInputDialog", result);
    }
}
