namespace UpdateChecker.Services;

internal interface IUpdateNotificationSink
{
    void ShowUpdatesFound(int updateCount, int majorUpdateCount);

    void ShowInformation(string title, string message);

    void ShowWarning(string title, string message);
}
