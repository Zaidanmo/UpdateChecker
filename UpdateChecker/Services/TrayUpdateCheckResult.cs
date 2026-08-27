using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal enum TrayUpdateCheckStatus
{
    Succeeded,
    Busy,
    TimedOut,
    Cancelled,
    Failed
}

internal sealed record TrayUpdateCheckResult(
    TrayUpdateCheckStatus Status,
    IReadOnlyList<AppUpdateInfo>? Updates = null,
    UpdateCheckFailure? Failure = null);
