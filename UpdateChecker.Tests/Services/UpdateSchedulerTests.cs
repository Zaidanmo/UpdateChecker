using System.Runtime.CompilerServices;
using UpdateChecker.Models;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class UpdateSchedulerTests
{
    [Fact]
    public void Dispose_DoesNotRemainRootedBySettingsStoreEvent()
    {
        var settingsStore = new TestSettingsStore(new UserSettings());
        WeakReference schedulerReference = CreateDisposedScheduler(
            settingsStore
        );

        ForceFullCollection();

        Assert.False(schedulerReference.IsAlive);
        Assert.Equal(0, settingsStore.SubscriberCount);
    }

    [Fact]
    public async Task StopAsync_AwaitsSchedulesReplacedBySettingsChanges()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var settingsStore = new TestSettingsStore(new UserSettings
        {
            RunInBackground = true,
            AutomaticChecksEnabled = true,
            AutomaticCheckInterval = AutomaticCheckInterval.Hourly,
            LastAutomaticCheckUtc = now.AddHours(-2)
        });
        var firstStarted = NewSignal();
        var secondStarted = NewSignal();
        int invocationCount = 0;
        int activeChecks = 0;

        async Task<ScheduledRunOutcome> RunCheck(
            CancellationToken cancellationToken)
        {
            int invocation = Interlocked.Increment(ref invocationCount);
            Interlocked.Increment(ref activeChecks);

            if (invocation == 1)
            {
                firstStarted.TrySetResult();
            }
            else if (invocation == 2)
            {
                secondStarted.TrySetResult();
            }

            try
            {
                await Task.Delay(
                    Timeout.InfiniteTimeSpan,
                    cancellationToken
                );
                return ScheduledRunOutcome.Attempted;
            }
            finally
            {
                Interlocked.Decrement(ref activeChecks);
            }
        }

        using var scheduler = new UpdateScheduler(
            settingsStore,
            RunCheck,
            () => now
        );

        scheduler.Start();
        await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        settingsStore.Change(settingsStore.Current with
        {
            AutomaticCheckInterval = AutomaticCheckInterval.EverySixHours,
            LastAutomaticCheckUtc = now.AddHours(-7)
        });
        await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await scheduler.StopAsync().WaitAsync(TimeSpan.FromSeconds(1));

        Assert.Equal(0, Volatile.Read(ref activeChecks));
        Assert.Equal(0, settingsStore.SubscriberCount);
    }

    private static TaskCompletionSource NewSignal()
    {
        return new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference CreateDisposedScheduler(
        TestSettingsStore settingsStore)
    {
        var scheduler = new UpdateScheduler(
            settingsStore,
            _ => Task.FromResult(ScheduledRunOutcome.Attempted)
        );
        scheduler.Start();
        scheduler.Dispose();
        return new WeakReference(scheduler);
    }

    private static void ForceFullCollection()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }

    private sealed class TestSettingsStore : IUserSettingsStore
    {
        private Action<UserSettings>? _settingsChanged;

        public TestSettingsStore(UserSettings settings)
        {
            Current = settings;
        }

        public UserSettings Current { get; private set; }

        public int SubscriberCount =>
            _settingsChanged?.GetInvocationList().Length ?? 0;

        public event Action<UserSettings>? SettingsChanged
        {
            add => _settingsChanged += value;
            remove => _settingsChanged -= value;
        }

        public void Change(UserSettings settings)
        {
            Current = settings;
            _settingsChanged?.Invoke(settings);
        }

        public void SetTheme(AppTheme theme) =>
            throw new NotSupportedException();

        public void SetRunInBackground(bool enabled) =>
            throw new NotSupportedException();

        public void SetAutomaticChecksEnabled(bool enabled) =>
            throw new NotSupportedException();

        public void SetAutomaticCheckInterval(
            AutomaticCheckInterval interval) =>
            throw new NotSupportedException();

        public void RecordAutomaticCheck(DateTimeOffset checkedAtUtc) =>
            throw new NotSupportedException();

        public void RecordSuccessfulCheck(DateTimeOffset checkedAtUtc) =>
            throw new NotSupportedException();

        public void RecordTrayCheckResult(
            DateTimeOffset checkedAtUtc,
            string? fingerprint) =>
            throw new NotSupportedException();

        public void RecordAutomaticCheckResult(
            DateTimeOffset checkedAtUtc,
            string? fingerprint) =>
            throw new NotSupportedException();
    }
}
