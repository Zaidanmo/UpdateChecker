using System.Windows;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal static class ThemeManager
{
    private const string ThemeDictionaryPrefix = "Themes/";
    private static IUserSettingsStore? _settingsStore;

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static event Action<AppTheme>? ThemeChanged;

    public static void Initialize(IUserSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        Apply(settingsStore.Current.Theme, persist: false);
    }

    public static void SetTheme(AppTheme theme)
    {
        if (theme != CurrentTheme)
        {
            Apply(theme, persist: true);
        }
    }

    private static void Apply(AppTheme theme, bool persist)
    {
        ResourceDictionary resources =
            System.Windows.Application.Current.Resources;
        ResourceDictionary newTheme = new()
        {
            Source = new Uri(
                $"{ThemeDictionaryPrefix}{theme}Theme.xaml",
                UriKind.Relative
            )
        };

        int currentThemeIndex = FindThemeDictionaryIndex(
            resources.MergedDictionaries
        );

        if (currentThemeIndex >= 0)
        {
            resources.MergedDictionaries[currentThemeIndex] = newTheme;
        }
        else
        {
            resources.MergedDictionaries.Insert(0, newTheme);
        }

        CurrentTheme = theme;
        ThemeChanged?.Invoke(theme);

        if (persist)
        {
            _settingsStore?.SetTheme(theme);
        }
    }

    private static int FindThemeDictionaryIndex(
        IList<ResourceDictionary> dictionaries)
    {
        for (int index = 0; index < dictionaries.Count; index++)
        {
            string? source = dictionaries[index].Source?.OriginalString;

            if (source?.EndsWith(
                    "LightTheme.xaml",
                    StringComparison.OrdinalIgnoreCase
                ) == true ||
                source?.EndsWith(
                    "DarkTheme.xaml",
                    StringComparison.OrdinalIgnoreCase
                ) == true)
            {
                return index;
            }
        }

        return -1;
    }

}
