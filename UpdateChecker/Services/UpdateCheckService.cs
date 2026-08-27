using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal sealed class UpdateCheckService
{
    internal static readonly TimeSpan DefaultTimeout =
        TimeSpan.FromSeconds(60);

    private readonly IUpdateSource _updateSource;
    private readonly TimeSpan _timeout;
    private int _isChecking;

    public UpdateCheckService(
        IUpdateSource updateSource,
        TimeSpan? timeout = null)
    {
        _updateSource = updateSource;
        _timeout = timeout ?? DefaultTimeout;
    }

    public async Task<IReadOnlyList<AppUpdateInfo>> CheckAsync(
        CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _isChecking, 1, 0) != 0)
        {
            throw new UpdateCheckInProgressException();
        }

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken
            );
        timeoutCancellation.CancelAfter(_timeout);

        try
        {
            return await _updateSource
                .GetAvailableUpdatesAsync(timeoutCancellation.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested &&
                  timeoutCancellation.IsCancellationRequested)
        {
            throw new UpdateCheckTimedOutException();
        }
        finally
        {
            Volatile.Write(ref _isChecking, 0);
        }
    }
}

internal sealed class UpdateCheckInProgressException : Exception;

internal sealed class UpdateCheckTimedOutException : Exception;
