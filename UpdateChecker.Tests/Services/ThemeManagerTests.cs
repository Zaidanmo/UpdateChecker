using UpdateChecker.Services;
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
    public void ParsePreference_ReturnsSafeTheme(
        string? value,
        string expected)
    {
        Assert.Equal(expected, ThemeManager.ParsePreference(value).ToString());
    }
}
