using System.Runtime.CompilerServices;
using UpdateChecker.Models;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class BackgroundUpdateServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task StopAsync_ReleasesEventSubscribers()
    {
        var settings = new UserSettingsManager(
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json"),
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt")
        );
        var service = new BackgroundUpdateService(
            new UpdateCheckService(new FixedUpdateSource([])),
            settings,
            new SilentNotificationSink()
        );
        WeakReference subscriberReference = SubscribeObserver(service);

        await service.StopAsync();
        ForceFullCollection();

        Assert.False(subscriberReference.IsAlive);
    }

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
    public void CalculateNextCheckUtc_DisabledScheduleHasNoNextCheck()
    {
        var settings = new UserSettings
        {
            RunInBackground = true,
            AutomaticChecksEnabled = false
        };

        DateTimeOffset? nextCheck = UpdateScheduler.CalculateNextCheckUtc(
            settings,
            Now
        );

        Assert.Null(nextCheck);
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
            var notificationSink = new SilentNotificationSink();
            var service = new BackgroundUpdateService(
                new UpdateCheckService(
                    new FixedUpdateSource([expectedUpdate])
                ),
                settings,
                notificationSink
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
            Assert.NotNull(settings.Current.LastSuccessfulCheckUtc);
            Assert.Equal(
                [
                    TrayIconStatus.Checking,
                    TrayIconStatus.UpdatesAvailable
                ],
                notificationSink.Statuses
            );
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

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference SubscribeObserver(
        BackgroundUpdateService service)
    {
        var observer = new BackgroundServiceObserver();
        service.UpdatesChecked += observer.OnUpdatesChecked;
        service.TrayCheckStarted += observer.OnTrayCheckStarted;
        service.TrayCheckCompleted += observer.OnTrayCheckCompleted;
        return new WeakReference(observer);
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class BackgroundServiceObserver
    {
        public void OnUpdatesChecked(IReadOnlyList<AppUpdateInfo> updates)
        {
        }

        public void OnTrayCheckStarted()
        {
        }

        public void OnTrayCheckCompleted(TrayUpdateCheckResult result)
        {
        }
    }

    private sealed class SilentNotificationSink : IUpdateNotificationSink
    {
        public List<TrayIconStatus> Statuses { get; } = [];

        public void SetStatus(TrayIconStatus status, int updateCount = 0)
        {
            Statuses.Add(status);
        }

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
