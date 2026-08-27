using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal interface IUpdateSource
{
    Task<IReadOnlyList<AppUpdateInfo>> GetAvailableUpdatesAsync(
        CancellationToken cancellationToken = default);
}
