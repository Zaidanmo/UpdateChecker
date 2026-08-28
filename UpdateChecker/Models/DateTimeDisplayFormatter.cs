using System.Globalization;

namespace UpdateChecker.Models;

internal static class DateTimeDisplayFormatter
{
    public static string FormatRelative(
        DateTimeOffset timestamp,
        DateTimeOffset now)
    {
        TimeSpan difference = timestamp - now;
        bool future = difference > TimeSpan.Zero;
        TimeSpan distance = difference.Duration();

        if (distance < TimeSpan.FromMinutes(1))
        {
            return future ? "in less than a minute" : "just now";
        }

        if (distance < TimeSpan.FromHours(1))
        {
            int minutes = Math.Max(1, (int)Math.Round(distance.TotalMinutes));
            return FormatUnit(minutes, "minute", future);
        }

        if (distance < TimeSpan.FromDays(1))
        {
            int hours = Math.Max(1, (int)Math.Round(distance.TotalHours));
            return FormatUnit(hours, "hour", future);
        }

        int days = Math.Max(1, (int)Math.Round(distance.TotalDays));
        return FormatUnit(days, "day", future);
    }

    public static string FormatExact(
        DateTimeOffset timestamp,
        CultureInfo? culture = null)
    {
        return timestamp
            .ToLocalTime()
            .ToString("F", culture ?? CultureInfo.CurrentCulture);
    }

    private static string FormatUnit(int value, string unit, bool future)
    {
        string quantity = $"{value} {unit}{(value == 1 ? "" : "s")}";
        return future ? $"in {quantity}" : $"{quantity} ago";
    }
}
