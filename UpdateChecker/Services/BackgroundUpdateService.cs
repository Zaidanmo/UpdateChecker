using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal sealed class BackgroundUpdateService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly UpdateCheckService _updateCheckService;
    private readonly IUserSettingsStore _settingsStore;
    private readonly UpdateNotificationPolicy _notificationPolicy;
    private readonly UpdateScheduler _scheduler;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly HashSet<Task> _activeTrayChecks = [];

    private bool _disposed;

    public BackgroundUpdateService(
        UpdateCheckService updateCheckService,
        IUserSettingsStore settingsStore,
        IUpdateNotificationSink notificationSink,
        Func<DateTimeOffset>? utcNow = null)
    {
        _updateCheckService = updateCheckService;
        _settingsStore = settingsStore;
        _notificationPolicy = new UpdateNotificationPolicy(notificationSink);
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _scheduler = new UpdateScheduler(
            settingsStore,
            RunScheduledCheckAsync,
            _utcNow
        );
    }

    public event Action<IReadOnlyList<AppUpdateInfo>>? UpdatesChecked;

    public event Action? TrayCheckStarted;

    public event Action<TrayUpdateCheckResult>? TrayCheckCompleted;

    public void Start()
    {
        _scheduler.Start();
    }

    public Task CheckNowFromTrayAsync()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return Task.CompletedTask;
            }

            TrayCheckStarted?.Invoke();
            Task checkTask = ExecuteCheckAsync(
                alwaysNotify: true,
                scheduled: false,
                _lifetimeCancellation.Token
            );
            _activeTrayChecks.Add(checkTask);
            _ = RemoveCompletedTrayCheckAsync(checkTask);
            return checkTask;
        }
    }

    public async Task StopAsync()
    {
        Task[] activeTrayChecks;

        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
            activeTrayChecks = [.. _activeTrayChecks];
        }

        await _scheduler.StopAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(activeTrayChecks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Active tray checks are cancelled during shutdown.
        }

        _lifetimeCancellation.Dispose();
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _lifetimeCancellation.Cancel();
        }

        _scheduler.Dispose();
    }

    private Task<ScheduledRunOutcome> RunScheduledCheckAsync(
        CancellationToken cancellationToken)
    {
        return ExecuteCheckAsync(
            alwaysNotify: false,
            scheduled: true,
            cancellationToken
        );
    }

    private async Task<ScheduledRunOutcome> ExecuteCheckAsync(
        bool alwaysNotify,
        bool scheduled,
        CancellationToken cancellationToken)
    {
        try
        {
            IReadOnlyList<AppUpdateInfo> updates =
                await _updateCheckService.CheckAsync(cancellationToken)
                    .ConfigureAwait(false);

            string? fingerprint = _notificationPolicy.NotifyForResult(
                updates,
                alwaysNotify,
                _settingsStore.Current.LastNotifiedUpdateFingerprint
            );

            if (scheduled)
            {
                _settingsStore.RecordAutomaticCheckResult(
                    _utcNow(),
                    fingerprint
                );
                UpdatesChecked?.Invoke(updates);
            }
            else
            {
                _settingsStore.RecordNotifiedUpdateFingerprint(fingerprint);
                TrayCheckCompleted?.Invoke(new TrayUpdateCheckResult(
                    TrayUpdateCheckStatus.Succeeded,
                    updates
                ));
            }

            return ScheduledRunOutcome.Attempted;
        }
        catch (UpdateCheckInProgressException)
        {
            if (alwaysNotify)
            {
                _notificationPolicy.NotifyCheckAlreadyRunning();
                TrayCheckCompleted?.Invoke(new TrayUpdateCheckResult(
                    TrayUpdateCheckStatus.Busy
                ));
            }

            return ScheduledRunOutcome.Busy;
        }
        catch (UpdateCheckTimedOutException)
        {
            _notificationPolicy.NotifyFailure(
                "Update check timed out",
                "WinGet took too long to respond. Try again later.",
                alwaysNotify
            );
            NotifyTrayCheckCompleted(
                scheduled,
                new TrayUpdateCheckResult(TrayUpdateCheckStatus.TimedOut)
            );
            RecordFailedScheduledAttempt(scheduled);
            return ScheduledRunOutcome.Attempted;
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            NotifyTrayCheckCompleted(
                scheduled,
                new TrayUpdateCheckResult(TrayUpdateCheckStatus.Cancelled)
            );
            return ScheduledRunOutcome.Cancelled;
        }
        catch (Exception exception)
        {
            UpdateCheckFailure failure =
                UpdateCheckErrorMapper.FromException(exception);
            _notificationPolicy.NotifyFailure(
                failure.Title,
                failure.NotificationMessage,
                alwaysNotify
            );
            NotifyTrayCheckCompleted(
                scheduled,
                new TrayUpdateCheckResult(
                    TrayUpdateCheckStatus.Failed,
                    Failure: failure
                )
            );
            RecordFailedScheduledAttempt(scheduled);
            return ScheduledRunOutcome.Attempted;
        }
    }

    private void RecordFailedScheduledAttempt(bool scheduled)
    {
        if (scheduled)
        {
            _settingsStore.RecordAutomaticCheck(_utcNow());
        }
    }

    private void NotifyTrayCheckCompleted(
        bool scheduled,
        TrayUpdateCheckResult result)
    {
        if (!scheduled)
        {
            TrayCheckCompleted?.Invoke(result);
        }
    }

    private async Task RemoveCompletedTrayCheckAsync(Task checkTask)
    {
        try
        {
            await checkTask.ConfigureAwait(false);
        }
        finally
        {
            lock (_syncRoot)
            {
                _activeTrayChecks.Remove(checkTask);
            }
        }
    }
}
