using System.Security.Cryptography;
using System.Text;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal sealed class UpdateNotificationPolicy
{
    private readonly IUpdateNotificationSink _notificationSink;
    private string? _lastFailureTitle;

    public UpdateNotificationPolicy(
        IUpdateNotificationSink notificationSink)
    {
        _notificationSink = notificationSink;
    }

    public string? NotifyForResult(
        IReadOnlyList<AppUpdateInfo> updates,
        bool alwaysNotify,
        string? lastFingerprint)
    {
        _lastFailureTitle = null;

        if (updates.Count == 0)
        {
            if (alwaysNotify)
            {
                _notificationSink.ShowInformation(
                    "You're up to date",
                    "No application updates were found."
                );
            }

            return null;
        }

        string fingerprint = CreateUpdateFingerprint(updates);
        bool hasChanged = !string.Equals(
            fingerprint,
            lastFingerprint,
            StringComparison.Ordinal
        );

        if (alwaysNotify || hasChanged)
        {
            int majorUpdateCount = updates.Count(
                update => update.HasMajorVersionChange
            );
            _notificationSink.ShowUpdatesFound(
                updates.Count,
                majorUpdateCount
            );
        }

        return fingerprint;
    }

    public void NotifyCheckAlreadyRunning()
    {
        _notificationSink.ShowInformation(
            "Update check already running",
            "App Update Checker is already scanning your applications."
        );
    }

    public void NotifyFailure(
        string title,
        string message,
        bool alwaysNotify)
    {
        if (!alwaysNotify &&
            string.Equals(_lastFailureTitle, title, StringComparison.Ordinal))
        {
            return;
        }

        _lastFailureTitle = title;
        _notificationSink.ShowWarning(title, message);
    }

    internal static string CreateUpdateFingerprint(
        IReadOnlyList<AppUpdateInfo> updates)
    {
        string normalized = string.Join(
            '\n',
            updates
                .OrderBy(update => update.Id, StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    update => update.AvailableVersion,
                    StringComparer.OrdinalIgnoreCase
                )
                .Select(update => $"{update.Id}\t{update.AvailableVersion}")
        );

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}
