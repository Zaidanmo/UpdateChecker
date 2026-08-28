using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal enum ScheduledRunOutcome
{
    Attempted,
    Busy,
    Cancelled
}

internal sealed class UpdateScheduler : IDisposable
{
    private static readonly TimeSpan InitialCheckDelay =
        TimeSpan.FromSeconds(10);

    private static readonly TimeSpan BusyRetryDelay =
        TimeSpan.FromMinutes(5);

    private readonly object _syncRoot = new();
    private readonly IUserSettingsStore _settingsStore;
    private readonly Func<CancellationToken, Task<ScheduledRunOutcome>>
        _runScheduledCheck;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly HashSet<Task> _activeScheduleTasks = [];

    private CancellationTokenSource? _scheduleCancellation;
    private SchedulePreferences _activePreferences;
    private bool _started;
    private bool _disposed;

    public UpdateScheduler(
        IUserSettingsStore settingsStore,
        Func<CancellationToken, Task<ScheduledRunOutcome>> runScheduledCheck,
        Func<DateTimeOffset>? utcNow = null)
    {
        _settingsStore = settingsStore;
        _runScheduledCheck = runScheduledCheck;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
    }

    public void Start()
    {
        lock (_syncRoot)
        {
            if (_started || _disposed)
            {
                return;
            }

            _started = true;
            _settingsStore.SettingsChanged += SettingsStore_SettingsChanged;
        }

        RestartIfScheduleChanged(_settingsStore.Current, force: true);
    }

    public async Task StopAsync()
    {
        CancellationTokenSource? cancellation;
        Task[] scheduleTasks;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _settingsStore.SettingsChanged -= SettingsStore_SettingsChanged;
            cancellation = _scheduleCancellation;
            _scheduleCancellation = null;
            scheduleTasks = [.. _activeScheduleTasks];
        }

        cancellation?.Cancel();

        try
        {
            await Task.WhenAll(scheduleTasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is the expected scheduler shutdown path.
        }
    }

    public void Dispose()
    {
        CancellationTokenSource? cancellation;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _settingsStore.SettingsChanged -= SettingsStore_SettingsChanged;
            cancellation = _scheduleCancellation;
            _scheduleCancellation = null;
        }

        cancellation?.Cancel();
    }

    internal static TimeSpan CalculateNextDelay(
        UserSettings settings,
        DateTimeOffset utcNow)
    {
        DateTimeOffset? nextCheck = CalculateNextCheckUtc(settings, utcNow);

        if (nextCheck is null)
        {
            return Timeout.InfiniteTimeSpan;
        }

        TimeSpan remaining =
            nextCheck.Value.ToUniversalTime() - utcNow.ToUniversalTime();

        return remaining > TimeSpan.Zero
            ? remaining
            : TimeSpan.Zero;
    }

    internal static DateTimeOffset? CalculateNextCheckUtc(
        UserSettings settings,
        DateTimeOffset utcNow)
    {
        if (!settings.AutomaticChecksEnabled)
        {
            return null;
        }

        if (settings.LastAutomaticCheckUtc is not DateTimeOffset lastCheck)
        {
            return utcNow.ToUniversalTime() + InitialCheckDelay;
        }

        DateTimeOffset nextCheck =
            lastCheck.ToUniversalTime() +
            settings.AutomaticCheckInterval.ToTimeSpan();

        return nextCheck > utcNow.ToUniversalTime()
            ? nextCheck
            : utcNow.ToUniversalTime();
    }

    private void SettingsStore_SettingsChanged(UserSettings settings)
    {
        RestartIfScheduleChanged(settings, force: false);
    }

    private void RestartIfScheduleChanged(
        UserSettings settings,
        bool force)
    {
        SchedulePreferences preferences = new(
            settings.AutomaticChecksEnabled,
            settings.AutomaticCheckInterval
        );
        CancellationTokenSource? previousCancellation;

        lock (_syncRoot)
        {
            if (_disposed ||
                (!force && preferences == _activePreferences))
            {
                return;
            }

            _activePreferences = preferences;
            previousCancellation = _scheduleCancellation;
            _scheduleCancellation = null;

            if (preferences.Enabled)
            {
                var newCancellation = new CancellationTokenSource();
                _scheduleCancellation = newCancellation;
                Task scheduleTask =
                    RunScheduleAndDisposeAsync(newCancellation);
                _activeScheduleTasks.Add(scheduleTask);
                _ = RemoveCompletedScheduleAsync(scheduleTask);
            }
        }

        previousCancellation?.Cancel();
    }

    private async Task RunScheduleAndDisposeAsync(
        CancellationTokenSource cancellation)
    {
        try
        {
            await RunScheduleAsync(cancellation.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (cancellation.IsCancellationRequested)
        {
            // The schedule was disabled or replaced.
        }
        finally
        {
            cancellation.Dispose();
        }
    }

    private async Task RemoveCompletedScheduleAsync(Task scheduleTask)
    {
        try
        {
            await scheduleTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Cancellation is already handled by the schedule wrapper.
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeScheduleTasks.Remove(scheduleTask);
            }
        }
    }

    private async Task RunScheduleAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            UserSettings settings = _settingsStore.Current;

            if (!settings.AutomaticChecksEnabled)
            {
                return;
            }

            TimeSpan delay = CalculateNextDelay(settings, _utcNow());
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);

            ScheduledRunOutcome outcome =
                await _runScheduledCheck(cancellationToken)
                    .ConfigureAwait(false);

            if (outcome == ScheduledRunOutcome.Cancelled)
            {
                return;
            }

            if (outcome == ScheduledRunOutcome.Busy)
            {
                await Task.Delay(BusyRetryDelay, cancellationToken)
                    .ConfigureAwait(false);
            }
        }
    }

    private readonly record struct SchedulePreferences(
        bool Enabled,
        AutomaticCheckInterval Interval);
}
