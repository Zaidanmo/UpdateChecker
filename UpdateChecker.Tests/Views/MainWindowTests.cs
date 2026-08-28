using UpdateChecker.Models;
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
            DateTimeOffset.UtcNow
        );

        Assert.Equal("Last checked: Never", status);
    }

    [Fact]
    public void FormatLastSuccessfulCheck_WithHistoryShowsRelativeTime()
    {
        var now = new DateTimeOffset(
            2026,
            8,
            27,
            12,
            30,
            0,
            TimeSpan.Zero
        );
        string status = MainWindow.FormatLastSuccessfulCheck(
            now.AddMinutes(-8),
            now
        );

        Assert.Equal("Last checked: 8 minutes ago", status);
    }

    [Fact]
    public void CreateUpgradeCommand_QuotesAndSanitizesPackageId()
    {
        var update = new AppUpdateInfo(
            "Example",
            "Vendor.\"Example",
            "1.0",
            "1.1"
        );

        string command = MainWindow.CreateUpgradeCommand(update);

        Assert.Equal(
            "winget upgrade --id \"Vendor.Example\" --exact " +
            "--accept-source-agreements --accept-package-agreements",
            command
        );
    }
}
