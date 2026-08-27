namespace UpdateChecker.Models;

internal enum AppTheme
{
    Light,
    Dark
}

internal static class AppThemeParser
{
    public static AppTheme Parse(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out AppTheme theme)
            ? theme
            : AppTheme.Light;
    }
}
