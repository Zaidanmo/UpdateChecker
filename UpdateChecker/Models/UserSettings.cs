namespace UpdateChecker.Models;

internal enum AutomaticCheckInterval
{
    Hourly,
    EverySixHours,
    EveryTwelveHours,
    Daily,
    Weekly
}

internal sealed record UserSettings
{
    public AppTheme Theme { get; init; } = AppTheme.Light;

    public bool RunInBackground { get; init; }

    public bool AutomaticChecksEnabled { get; init; }

    public AutomaticCheckInterval AutomaticCheckInterval { get; init; } =
        AutomaticCheckInterval.Daily;

    public DateTimeOffset? LastAutomaticCheckUtc { get; init; }

    public DateTimeOffset? LastSuccessfulCheckUtc { get; init; }

    public string? LastNotifiedUpdateFingerprint { get; init; }
}

internal static class AutomaticCheckIntervalExtensions
{
    public static TimeSpan ToTimeSpan(this AutomaticCheckInterval interval)
    {
        return interval switch
        {
            AutomaticCheckInterval.Hourly => TimeSpan.FromHours(1),
            AutomaticCheckInterval.EverySixHours => TimeSpan.FromHours(6),
            AutomaticCheckInterval.EveryTwelveHours => TimeSpan.FromHours(12),
            AutomaticCheckInterval.Daily => TimeSpan.FromDays(1),
            AutomaticCheckInterval.Weekly => TimeSpan.FromDays(7),
            _ => TimeSpan.FromDays(1)
        };
    }
}
