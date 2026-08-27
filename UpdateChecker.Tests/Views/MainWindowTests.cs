using System.Globalization;
using UpdateChecker.Views;
using Xunit;

namespace UpdateChecker.Tests.Views;

public sealed class MainWindowTests
{
    [Fact]
    public void FormatLastSuccessfulCheck_WithoutHistoryShowsNever()
    {
        string status = MainWindow.FormatLastSuccessfulCheck(
            null,
            CultureInfo.InvariantCulture
        );

        Assert.Equal("Last checked: Never", status);
    }

    [Fact]
    public void FormatLastSuccessfulCheck_WithHistoryShowsTimestamp()
    {
        string status = MainWindow.FormatLastSuccessfulCheck(
            new DateTimeOffset(2026, 8, 27, 12, 30, 0, TimeSpan.Zero),
            CultureInfo.InvariantCulture
        );

        Assert.StartsWith("Last checked: ", status, StringComparison.Ordinal);
        Assert.Contains("2026", status, StringComparison.Ordinal);
    }
}
