namespace UpdateChecker.Services;

internal enum TrayIconStatus
{
    Ready,
    Checking,
    UpToDate,
    UpdatesAvailable,
    Failed
}

internal interface IUpdateNotificationSink
{
    void SetStatus(TrayIconStatus status, int updateCount = 0);

    void ShowUpdatesFound(int updateCount, int majorUpdateCount);

    void ShowInformation(string title, string message);

    void ShowWarning(string title, string message);
}
