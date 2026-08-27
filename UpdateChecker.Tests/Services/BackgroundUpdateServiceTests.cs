using UpdateChecker.Models;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class BackgroundUpdateServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CalculateNextDelay_NewScheduleStartsShortly()
    {
        var settings = new UserSettings
        {
            RunInBackground = true,
            AutomaticChecksEnabled = true
        };

        TimeSpan delay = UpdateScheduler.CalculateNextDelay(
            settings,
            Now
        );

        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void CalculateNextDelay_UsesTimeRemainingInInterval()
    {
        var settings = new UserSettings
        {
            RunInBackground = true,
            AutomaticChecksEnabled = true,
            AutomaticCheckInterval = AutomaticCheckInterval.Daily,
            LastAutomaticCheckUtc = Now.AddHours(-6)
        };

        TimeSpan delay = UpdateScheduler.CalculateNextDelay(
            settings,
            Now
        );

        Assert.Equal(TimeSpan.FromHours(18), delay);
    }

    [Fact]
    public void CalculateNextDelay_OverdueScheduleRunsImmediately()
    {
        var settings = new UserSettings
        {
            RunInBackground = true,
            AutomaticChecksEnabled = true,
            AutomaticCheckInterval = AutomaticCheckInterval.Hourly,
            LastAutomaticCheckUtc = Now.AddHours(-2)
        };

        TimeSpan delay = UpdateScheduler.CalculateNextDelay(
            settings,
            Now
        );

        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void CreateUpdateFingerprint_IsIndependentOfResultOrder()
    {
        AppUpdateInfo first = new("First", "Vendor.First", "1.0", "2.0");
        AppUpdateInfo second = new("Second", "Vendor.Second", "3.0", "3.1");

        string forward = UpdateNotificationPolicy.CreateUpdateFingerprint(
            [first, second]
        );
        string reverse = UpdateNotificationPolicy.CreateUpdateFingerprint(
            [second, first]
        );

        Assert.Equal(forward, reverse);
    }

    [Fact]
    public void CreateUpdateFingerprint_ChangesWithAvailableVersion()
    {
        string original = UpdateNotificationPolicy.CreateUpdateFingerprint(
            [new AppUpdateInfo("App", "Vendor.App", "1.0", "1.1")]
        );
        string changed = UpdateNotificationPolicy.CreateUpdateFingerprint(
            [new AppUpdateInfo("App", "Vendor.App", "1.0", "1.2")]
        );

        Assert.NotEqual(original, changed);
    }

    [Fact]
    public async Task CheckNowFromTrayAsync_ReportsVisibleUiLifecycle()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"UpdateChecker.Tests.{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directory);

        try
        {
            var expectedUpdate = new AppUpdateInfo(
                "Example",
                "Vendor.Example",
                "1.0",
                "1.1"
            );
            var settings = new UserSettingsManager(
                Path.Combine(directory, "settings.json"),
                Path.Combine(directory, "theme.txt")
            );
            settings.Initialize();
            var service = new BackgroundUpdateService(
                new UpdateCheckService(
                    new FixedUpdateSource([expectedUpdate])
                ),
                settings,
                new SilentNotificationSink()
            );
            var lifecycle = new List<string>();
            TrayUpdateCheckResult? completion = null;
            service.TrayCheckStarted += () => lifecycle.Add("started");
            service.TrayCheckCompleted += result =>
            {
                completion = result;
                lifecycle.Add("completed");
            };

            await service.CheckNowFromTrayAsync();
            await service.StopAsync();

            Assert.Equal(["started", "completed"], lifecycle);
            Assert.NotNull(completion);
            Assert.Equal(
                TrayUpdateCheckStatus.Succeeded,
                completion.Status
            );
            Assert.Equal([expectedUpdate], completion.Updates);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class FixedUpdateSource : IUpdateSource
    {
        private readonly IReadOnlyList<AppUpdateInfo> _updates;

        public FixedUpdateSource(IReadOnlyList<AppUpdateInfo> updates)
        {
            _updates = updates;
        }

        public Task<IReadOnlyList<AppUpdateInfo>> GetAvailableUpdatesAsync(
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_updates);
        }
    }

    private sealed class SilentNotificationSink : IUpdateNotificationSink
    {
        public void ShowUpdatesFound(int updateCount, int majorUpdateCount)
        {
        }

        public void ShowInformation(string title, string message)
        {
        }

        public void ShowWarning(string title, string message)
        {
        }
    }
}
