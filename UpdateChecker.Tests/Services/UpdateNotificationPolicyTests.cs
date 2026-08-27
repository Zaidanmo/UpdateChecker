using UpdateChecker.Models;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class UpdateNotificationPolicyTests
{
    [Fact]
    public void NotifyForResult_SuppressesUnchangedScheduledResult()
    {
        var sink = new RecordingNotificationSink();
        var policy = new UpdateNotificationPolicy(sink);
        AppUpdateInfo[] updates =
        [
            new("Example", "Vendor.Example", "1.0", "1.1")
        ];
        string fingerprint =
            UpdateNotificationPolicy.CreateUpdateFingerprint(updates);

        string? result = policy.NotifyForResult(
            updates,
            alwaysNotify: false,
            fingerprint
        );

        Assert.Equal(fingerprint, result);
        Assert.Equal(0, sink.UpdatesFoundCount);
    }

    [Fact]
    public void NotifyFailure_SuppressesRepeatedScheduledFailure()
    {
        var sink = new RecordingNotificationSink();
        var policy = new UpdateNotificationPolicy(sink);

        policy.NotifyFailure("WinGet unavailable", "First", false);
        policy.NotifyFailure("WinGet unavailable", "Second", false);

        Assert.Equal(1, sink.WarningCount);
    }

    [Fact]
    public void NotifyFailure_AlwaysShowsManualFailure()
    {
        var sink = new RecordingNotificationSink();
        var policy = new UpdateNotificationPolicy(sink);

        policy.NotifyFailure("WinGet unavailable", "First", false);
        policy.NotifyFailure("WinGet unavailable", "Second", true);

        Assert.Equal(2, sink.WarningCount);
    }

    private sealed class RecordingNotificationSink : IUpdateNotificationSink
    {
        public int UpdatesFoundCount { get; private set; }

        public int WarningCount { get; private set; }

        public void ShowUpdatesFound(int updateCount, int majorUpdateCount)
        {
            UpdatesFoundCount++;
        }

        public void ShowInformation(string title, string message)
        {
        }

        public void ShowWarning(string title, string message)
        {
            WarningCount++;
        }
    }
}
