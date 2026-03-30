using Xunit;

namespace DirectumMcp.Tests;

public class DiagnoseBuildErrorTests
{
    private readonly DirectumMcp.DevTools.Tools.DiagnoseBuildErrorTool _tool = new();

    [Theory]
    [InlineData("Missing area in BaseGenerator", "CollectionPropertyMetadata")]
    [InlineData("NullReferenceException at InterfacesGenerator", "CollectionPropertyMetadata")]
    [InlineData("Reserved word 'new' is not a valid identifier", "зарезервированное слово")]
    [InlineData("CS0116 reserved word error", "зарезервированное слово")]
    [InlineData("Duplicate column 'Deal' already exists", "Дублирующийся Code")]
    [InlineData("File lock on Project.csproj", "заблокирован")]
    [InlineData("access denied to file.csproj", "заблокирован")]
    [InlineData("Invalid ResX input format", "формат .resx")]
    [InlineData("Недопустимый ввод в файле resx", "формат .resx")]
    [InlineData("AttachmentGroup Constraint mismatch", "AttachmentGroup")]
    [InlineData("Analyzers directory not found in .sds", "Analyzers")]
    [InlineData("Can't resolve function ShowDeals", "FunctionName")]
    [InlineData("FormTabs not supported in DDS 25.3", "FormTabs")]
    [InlineData("DomainApi Version missing", "DomainApi")]
    public async Task Diagnose_RecognizesKnownError(string errorText, string expectedKeyword)
    {
        var result = await _tool.DiagnoseBuildError(errorText);

        Assert.Contains("Найдено совпадений", result);
        Assert.Contains(expectedKeyword, result);
        Assert.Contains("Исправление:", result);
    }

    [Fact]
    public async Task Diagnose_UnknownError_GivesRecommendations()
    {
        var result = await _tool.DiagnoseBuildError("Some completely unknown error xyz123");

        Assert.Contains("Неизвестная ошибка", result);
        Assert.Contains("check_package", result);
        Assert.Contains("trace_errors", result);
    }

    [Fact]
    public async Task Diagnose_MultipleMatches_ShowsAll()
    {
        // Error mentioning both file lock and resx
        var result = await _tool.DiagnoseBuildError("File lock on .csproj AND invalid ResX format error");

        Assert.Contains("Найдено совпадений", result);
        // Should match both patterns
        var matchCount = result.Split("###").Length - 1;
        Assert.True(matchCount >= 2, $"Expected >=2 matches, got {matchCount}");
    }

    [Fact]
    public async Task Diagnose_EmptyError_HandlesGracefully()
    {
        var result = await _tool.DiagnoseBuildError("");
        Assert.Contains("Неизвестная ошибка", result);
    }

    [Fact]
    public async Task Diagnose_LongError_Truncates()
    {
        var longError = new string('x', 500);
        var result = await _tool.DiagnoseBuildError(longError);
        Assert.Contains("...", result); // Truncated
    }

    [Fact]
    public async Task Diagnose_HasFixRecommendation()
    {
        var result = await _tool.DiagnoseBuildError("Missing area error in BaseGenerator");
        Assert.Contains("fix_package", result);
    }
}
