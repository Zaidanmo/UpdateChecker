using System.IO;
using System.Security;
using Microsoft.Win32;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal static class SystemThemeService
{
    private const string PersonalizationRegistryPath =
        @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static AppTheme GetPreferredAppTheme()
    {
        try
        {
            object? value = Registry.GetValue(
                PersonalizationRegistryPath,
                "AppsUseLightTheme",
                defaultValue: 1
            );

            return value is int themeValue && themeValue == 0
                ? AppTheme.Dark
                : AppTheme.Light;
        }
        catch (IOException)
        {
            return AppTheme.Light;
        }
        catch (UnauthorizedAccessException)
        {
            return AppTheme.Light;
        }
        catch (SecurityException)
        {
            return AppTheme.Light;
        }
    }
}
