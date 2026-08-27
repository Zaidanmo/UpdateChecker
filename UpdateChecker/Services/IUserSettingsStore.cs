using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal interface IUserSettingsStore
{
    UserSettings Current { get; }

    event Action<UserSettings>? SettingsChanged;

    void SetTheme(AppTheme theme);

    void SetRunInBackground(bool enabled);

    void SetAutomaticChecksEnabled(bool enabled);

    void SetAutomaticCheckInterval(AutomaticCheckInterval interval);

    void RecordAutomaticCheck(DateTimeOffset checkedAtUtc);

    void RecordSuccessfulCheck(DateTimeOffset checkedAtUtc);

    void RecordTrayCheckResult(
        DateTimeOffset checkedAtUtc,
        string? fingerprint);

    void RecordAutomaticCheckResult(
        DateTimeOffset checkedAtUtc,
        string? fingerprint);
}
