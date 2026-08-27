using UpdateChecker.Models;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class ThemeManagerTests
{
    [Theory]
    [InlineData("Light", "Light")]
    [InlineData("light", "Light")]
    [InlineData("Dark", "Dark")]
    [InlineData("dark", "Dark")]
    [InlineData("unsupported", "Light")]
    [InlineData(null, "Light")]
    public void Parse_ReturnsSafeTheme(
        string? value,
        string expected)
    {
        Assert.Equal(expected, AppThemeParser.Parse(value).ToString());
    }
}
