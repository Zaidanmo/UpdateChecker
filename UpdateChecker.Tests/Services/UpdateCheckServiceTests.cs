using UpdateChecker.Models;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class UpdateCheckServiceTests
{
    [Fact]
    public async Task CheckAsync_RejectsConcurrentChecks()
    {
        var source = new BlockingUpdateSource();
        var service = new UpdateCheckService(
            source,
            TimeSpan.FromSeconds(5)
        );

        Task<IReadOnlyList<AppUpdateInfo>> firstCheck =
            service.CheckAsync();
        await source.Started;

        await Assert.ThrowsAsync<UpdateCheckInProgressException>(
            () => service.CheckAsync()
        );

        source.Complete();
        Assert.Empty(await firstCheck);
    }

    [Fact]
    public async Task CheckAsync_MapsInternalTimeoutToSpecificException()
    {
        var service = new UpdateCheckService(
            new NeverCompletingUpdateSource(),
            TimeSpan.FromMilliseconds(25)
        );

        await Assert.ThrowsAsync<UpdateCheckTimedOutException>(
            () => service.CheckAsync()
        );
    }

    [Fact]
    public async Task CheckAsync_PreservesCallerCancellation()
    {
        var service = new UpdateCheckService(
            new NeverCompletingUpdateSource(),
            TimeSpan.FromSeconds(5)
        );
        using var cancellation = new CancellationTokenSource();

        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.CheckAsync(cancellation.Token)
        );
    }

    private sealed class BlockingUpdateSource : IUpdateSource
    {
        private readonly TaskCompletionSource _started = new(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        private readonly TaskCompletionSource<IReadOnlyList<AppUpdateInfo>>
            _completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

        public Task Started => _started.Task;

        public async Task<IReadOnlyList<AppUpdateInfo>>
            GetAvailableUpdatesAsync(
                CancellationToken cancellationToken = default)
        {
            _started.TrySetResult();
            return await _completion.Task.WaitAsync(cancellationToken);
        }

        public void Complete()
        {
            _completion.TrySetResult([]);
        }
    }

    private sealed class NeverCompletingUpdateSource : IUpdateSource
    {
        public async Task<IReadOnlyList<AppUpdateInfo>>
            GetAvailableUpdatesAsync(
                CancellationToken cancellationToken = default)
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                cancellationToken
            );
            return [];
        }
    }
}
