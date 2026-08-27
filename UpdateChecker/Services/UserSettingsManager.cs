using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal sealed class UserSettingsManager : IUserSettingsStore
{
    private const string SettingsFileName = "settings.json";
    private const string LegacyThemeFileName = "theme.txt";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly object _syncRoot = new();
    private readonly string _settingsFilePath;
    private readonly string _legacyThemeFilePath;
    private UserSettings _current = new();

    public UserSettingsManager(
        string? settingsFilePath = null,
        string? legacyThemeFilePath = null)
    {
        string settingsDirectory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            ),
            "UpdateChecker"
        );

        _settingsFilePath = settingsFilePath ?? Path.Combine(
            settingsDirectory,
            SettingsFileName
        );
        _legacyThemeFilePath = legacyThemeFilePath ?? Path.Combine(
            settingsDirectory,
            LegacyThemeFileName
        );
    }

    public UserSettings Current
    {
        get
        {
            lock (_syncRoot)
            {
                return _current;
            }
        }
    }

    public event Action<UserSettings>? SettingsChanged;

    public void Initialize()
    {
        lock (_syncRoot)
        {
            LoadResult loaded = Load();
            _current = loaded.Settings;

            if (loaded.MigratedLegacyTheme && Save(_current))
            {
                DeleteLegacyThemePreference();
            }
        }
    }

    public void SetTheme(AppTheme theme)
    {
        UpdatePreferences(settings => settings with
        {
            Theme = Enum.IsDefined(theme) ? theme : AppTheme.Light
        });
    }

    public void SetRunInBackground(bool enabled)
    {
        UpdatePreferences(settings => settings with
        {
            RunInBackground = enabled,
            AutomaticChecksEnabled = enabled &&
                                     settings.AutomaticChecksEnabled
        });
    }

    public void SetAutomaticChecksEnabled(bool enabled)
    {
        UpdatePreferences(settings => settings with
        {
            AutomaticChecksEnabled = enabled,
            RunInBackground = enabled || settings.RunInBackground,
            LastAutomaticCheckUtc = enabled
                ? settings.LastAutomaticCheckUtc
                : null,
            LastNotifiedUpdateFingerprint = enabled
                ? settings.LastNotifiedUpdateFingerprint
                : null
        });
    }

    public void SetAutomaticCheckInterval(
        AutomaticCheckInterval interval)
    {
        UpdatePreferences(settings => settings with
        {
            AutomaticCheckInterval = Enum.IsDefined(interval)
                ? interval
                : AutomaticCheckInterval.Daily
        });
    }

    public void RecordAutomaticCheck(DateTimeOffset checkedAtUtc)
    {
        UpdateMetadata(settings => settings with
        {
            LastAutomaticCheckUtc = checkedAtUtc.ToUniversalTime()
        });
    }

    public void RecordNotifiedUpdateFingerprint(string? fingerprint)
    {
        UpdateMetadata(settings => settings with
        {
            LastNotifiedUpdateFingerprint = fingerprint
        });
    }

    public void RecordAutomaticCheckResult(
        DateTimeOffset checkedAtUtc,
        string? fingerprint)
    {
        UpdateMetadata(settings => settings with
        {
            LastAutomaticCheckUtc = checkedAtUtc.ToUniversalTime(),
            LastNotifiedUpdateFingerprint = fingerprint
        });
    }

    internal static UserSettings Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new UserSettings();
        }

        try
        {
            UserSettings settings =
                JsonSerializer.Deserialize<UserSettings>(
                    json,
                    SerializerOptions
                ) ?? new UserSettings();

            return Normalize(settings);
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
    }

    internal static string Serialize(UserSettings settings)
    {
        return JsonSerializer.Serialize(Normalize(settings), SerializerOptions);
    }

    private void UpdatePreferences(
        Func<UserSettings, UserSettings> update)
    {
        UserSettings normalized;
        Action<UserSettings>? settingsChanged;

        lock (_syncRoot)
        {
            normalized = Normalize(update(_current));

            if (normalized == _current)
            {
                return;
            }

            _current = normalized;
            _ = Save(_current);
            settingsChanged = SettingsChanged;
        }

        settingsChanged?.Invoke(normalized);
    }

    private void UpdateMetadata(
        Func<UserSettings, UserSettings> update)
    {
        lock (_syncRoot)
        {
            UserSettings normalized = Normalize(update(_current));

            if (normalized == _current)
            {
                return;
            }

            _current = normalized;
            _ = Save(_current);
        }
    }

    private static UserSettings Normalize(UserSettings settings)
    {
        return settings with
        {
            Theme = Enum.IsDefined(settings.Theme)
                ? settings.Theme
                : AppTheme.Light,
            AutomaticChecksEnabled =
                settings.AutomaticChecksEnabled && settings.RunInBackground,
            AutomaticCheckInterval =
                Enum.IsDefined(settings.AutomaticCheckInterval)
                    ? settings.AutomaticCheckInterval
                    : AutomaticCheckInterval.Daily,
            LastAutomaticCheckUtc = settings.LastAutomaticCheckUtc?
                .ToUniversalTime(),
            LastNotifiedUpdateFingerprint = string.IsNullOrWhiteSpace(
                settings.LastNotifiedUpdateFingerprint
            )
                ? null
                : settings.LastNotifiedUpdateFingerprint
        };
    }

    private LoadResult Load()
    {
        try
        {
            string? json = File.Exists(_settingsFilePath)
                ? File.ReadAllText(_settingsFilePath)
                : null;
            UserSettings settings = Deserialize(json);

            if (!ContainsThemeProperty(json) &&
                TryReadLegacyTheme(out AppTheme legacyTheme))
            {
                return new LoadResult(
                    settings with { Theme = legacyTheme },
                    MigratedLegacyTheme: true
                );
            }

            return new LoadResult(
                settings,
                MigratedLegacyTheme: false
            );
        }
        catch (IOException)
        {
            return new LoadResult(new UserSettings(), false);
        }
        catch (UnauthorizedAccessException)
        {
            return new LoadResult(new UserSettings(), false);
        }
    }

    private bool Save(UserSettings settings)
    {
        string? directory = Path.GetDirectoryName(_settingsFilePath);
        string temporaryPath =
            $"{_settingsFilePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            if (directory is not null)
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(temporaryPath, Serialize(settings));
            File.Move(temporaryPath, _settingsFilePath, overwrite: true);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            TryDeleteFile(temporaryPath);
        }
    }

    private bool TryReadLegacyTheme(out AppTheme theme)
    {
        try
        {
            if (File.Exists(_legacyThemeFilePath))
            {
                theme = AppThemeParser.Parse(
                    File.ReadAllText(_legacyThemeFilePath).Trim()
                );
                return true;
            }
        }
        catch (IOException)
        {
            // Fall back to the default theme when migration cannot read.
        }
        catch (UnauthorizedAccessException)
        {
            // Fall back to the default theme when migration cannot read.
        }

        theme = AppTheme.Light;
        return false;
    }

    private void DeleteLegacyThemePreference()
    {
        TryDeleteFile(_legacyThemeFilePath);
    }

    private static bool ContainsThemeProperty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("Theme", out _);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // A stale migration or temporary file is harmless.
        }
        catch (UnauthorizedAccessException)
        {
            // A stale migration or temporary file is harmless.
        }
    }

    private readonly record struct LoadResult(
        UserSettings Settings,
        bool MigratedLegacyTheme);
}
