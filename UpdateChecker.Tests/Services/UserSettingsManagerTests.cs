using UpdateChecker.Models;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class UserSettingsManagerTests
{
    [Theory]
    [InlineData("Hourly", 1)]
    [InlineData("EverySixHours", 6)]
    [InlineData("EveryTwelveHours", 12)]
    [InlineData("Daily", 24)]
    [InlineData("Weekly", 168)]
    public void CheckInterval_MapsToExpectedHours(
        string intervalName,
        double expectedHours)
    {
        AutomaticCheckInterval interval = Enum.Parse<AutomaticCheckInterval>(
            intervalName
        );

        Assert.Equal(expectedHours, interval.ToTimeSpan().TotalHours);
    }

    [Fact]
    public void SerializeAndDeserialize_PreservesPreferences()
    {
        var expected = new UserSettings
        {
            Theme = AppTheme.Dark,
            RunInBackground = true,
            AutomaticChecksEnabled = true,
            AutomaticCheckInterval =
                AutomaticCheckInterval.EveryTwelveHours,
            LastAutomaticCheckUtc =
                new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero),
            LastSuccessfulCheckUtc =
                new DateTimeOffset(2026, 8, 27, 10, 0, 0, TimeSpan.Zero),
            LastNotifiedUpdateFingerprint = "ABC123"
        };

        UserSettings actual = UserSettingsManager.Deserialize(
            UserSettingsManager.Serialize(expected)
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Deserialize_InvalidJson_ReturnsSafeDefaults()
    {
        UserSettings settings = UserSettingsManager.Deserialize("{invalid");

        Assert.False(settings.RunInBackground);
        Assert.False(settings.AutomaticChecksEnabled);
        Assert.Equal(
            AutomaticCheckInterval.Daily,
            settings.AutomaticCheckInterval
        );
    }

    [Fact]
    public void Deserialize_DisablesAutomaticChecksWithoutBackgroundMode()
    {
        const string json = """
            {
              "RunInBackground": false,
              "AutomaticChecksEnabled": true,
              "AutomaticCheckInterval": "Hourly"
            }
            """;

        UserSettings settings = UserSettingsManager.Deserialize(json);

        Assert.False(settings.AutomaticChecksEnabled);
    }

    [Fact]
    public void Initialize_MigratesLegacyThemeIntoSettingsFile()
    {
        string directory = CreateTemporaryDirectory();
        string settingsPath = Path.Combine(directory, "settings.json");
        string legacyThemePath = Path.Combine(directory, "theme.txt");

        try
        {
            File.WriteAllText(
                settingsPath,
                """
                {
                  "RunInBackground": false,
                  "AutomaticChecksEnabled": false,
                  "AutomaticCheckInterval": "Daily"
                }
                """
            );
            File.WriteAllText(legacyThemePath, "Dark");

            var manager = new UserSettingsManager(
                settingsPath,
                legacyThemePath
            );

            manager.Initialize();

            Assert.Equal(AppTheme.Dark, manager.Current.Theme);
            Assert.False(File.Exists(legacyThemePath));
            Assert.Contains(
                "\"Theme\": \"Dark\"",
                File.ReadAllText(settingsPath),
                StringComparison.Ordinal
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_FirstRunUsesSystemThemeAndSafeDefaults()
    {
        string directory = CreateTemporaryDirectory();
        string settingsPath = Path.Combine(directory, "settings.json");
        string legacyThemePath = Path.Combine(directory, "theme.txt");

        try
        {
            var manager = new UserSettingsManager(
                settingsPath,
                legacyThemePath,
                systemThemeProvider: () => AppTheme.Dark
            );

            manager.Initialize();

            Assert.Equal(AppTheme.Dark, manager.Current.Theme);
            Assert.False(manager.Current.RunInBackground);
            Assert.False(manager.Current.AutomaticChecksEnabled);
            Assert.True(File.Exists(settingsPath));
            Assert.Contains(
                "\"Theme\": \"Dark\"",
                File.ReadAllText(settingsPath),
                StringComparison.Ordinal
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Initialize_ExistingThemeIsNotReplacedBySystemTheme()
    {
        string directory = CreateTemporaryDirectory();
        string settingsPath = Path.Combine(directory, "settings.json");
        string legacyThemePath = Path.Combine(directory, "theme.txt");

        try
        {
            File.WriteAllText(
                settingsPath,
                UserSettingsManager.Serialize(new UserSettings
                {
                    Theme = AppTheme.Light
                })
            );
            var manager = new UserSettingsManager(
                settingsPath,
                legacyThemePath,
                systemThemeProvider: () => AppTheme.Dark
            );

            manager.Initialize();

            Assert.Equal(AppTheme.Light, manager.Current.Theme);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RecordAutomaticCheckResult_PersistsMetadataTogether()
    {
        string directory = CreateTemporaryDirectory();
        string settingsPath = Path.Combine(directory, "settings.json");
        string legacyThemePath = Path.Combine(directory, "theme.txt");
        var checkedAt = new DateTimeOffset(
            2026,
            8,
            27,
            12,
            30,
            0,
            TimeSpan.FromHours(2)
        );

        try
        {
            var manager = new UserSettingsManager(
                settingsPath,
                legacyThemePath
            );
            manager.Initialize();
            manager.SetRunInBackground(true);
            manager.SetAutomaticChecksEnabled(true);

            manager.RecordAutomaticCheckResult(checkedAt, "ABC123");

            var reloaded = new UserSettingsManager(
                settingsPath,
                legacyThemePath
            );
            reloaded.Initialize();

            Assert.Equal(
                checkedAt.ToUniversalTime(),
                reloaded.Current.LastAutomaticCheckUtc
            );
            Assert.Equal(
                checkedAt.ToUniversalTime(),
                reloaded.Current.LastSuccessfulCheckUtc
            );
            Assert.Equal(
                "ABC123",
                reloaded.Current.LastNotifiedUpdateFingerprint
            );
            Assert.Empty(Directory.GetFiles(directory, "*.tmp"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            $"UpdateChecker.Tests.{Guid.NewGuid():N}"
        );
        Directory.CreateDirectory(directory);
        return directory;
    }
}
