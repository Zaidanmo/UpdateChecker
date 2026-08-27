using System.IO;
using System.Windows;

namespace UpdateChecker.Services;

internal enum AppTheme
{
    Light,
    Dark
}

internal static class ThemeManager
{
    private const string ThemeDictionaryPrefix = "Themes/";
    private const string PreferenceFileName = "theme.txt";

    public static AppTheme CurrentTheme { get; private set; } = AppTheme.Light;

    public static event Action<AppTheme>? ThemeChanged;

    public static void Initialize()
    {
        Apply(LoadPreference(), persist: false);
    }

    public static void SetTheme(AppTheme theme)
    {
        if (theme != CurrentTheme)
        {
            Apply(theme, persist: true);
        }
    }

    internal static AppTheme ParsePreference(string? value)
    {
        return Enum.TryParse(value, ignoreCase: true, out AppTheme theme)
            ? theme
            : AppTheme.Light;
    }

    private static void Apply(AppTheme theme, bool persist)
    {
        ResourceDictionary resources = Application.Current.Resources;
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
            SavePreference(theme);
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

    private static AppTheme LoadPreference()
    {
        try
        {
            return File.Exists(PreferenceFilePath)
                ? ParsePreference(File.ReadAllText(PreferenceFilePath).Trim())
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
    }

    private static void SavePreference(AppTheme theme)
    {
        try
        {
            string? directory = Path.GetDirectoryName(PreferenceFilePath);

            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(PreferenceFilePath, theme.ToString());
        }
        catch (IOException)
        {
            // Theme persistence is optional; the active theme still applies.
        }
        catch (UnauthorizedAccessException)
        {
            // Theme persistence is optional; the active theme still applies.
        }
    }

    private static string PreferenceFilePath => Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        ),
        "UpdateChecker",
        PreferenceFileName
    );
}
