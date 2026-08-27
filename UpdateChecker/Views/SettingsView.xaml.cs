using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using UpdateChecker.Models;
using UpdateChecker.Services;

namespace UpdateChecker.Views;

public partial class SettingsView : System.Windows.Controls.UserControl
{
    private static readonly string ApplicationFolderPath =
        Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);

    private bool _isSynchronizingChoices;

    public SettingsView()
    {
        InitializeComponent();
        ApplicationPathTextBox.Text = ApplicationFolderPath;
        ApplicationPathTextBox.ToolTip = ApplicationFolderPath;
    }

    public void RefreshChoices()
    {
        _isSynchronizingChoices = true;

        LightThemeRadioButton.IsChecked =
            ThemeManager.CurrentTheme == AppTheme.Light;
        DarkThemeRadioButton.IsChecked =
            ThemeManager.CurrentTheme == AppTheme.Dark;

        UserSettings settings = App.CurrentApp.UserSettings.Current;
        AutomaticChecksCheckBox.IsChecked =
            settings.AutomaticChecksEnabled;
        RunInBackgroundCheckBox.IsChecked = settings.RunInBackground;
        IntervalOptionsPanel.IsEnabled = settings.AutomaticChecksEnabled;

        HourlyIntervalRadioButton.IsChecked =
            settings.AutomaticCheckInterval == AutomaticCheckInterval.Hourly;
        SixHourIntervalRadioButton.IsChecked =
            settings.AutomaticCheckInterval ==
            AutomaticCheckInterval.EverySixHours;
        TwelveHourIntervalRadioButton.IsChecked =
            settings.AutomaticCheckInterval ==
            AutomaticCheckInterval.EveryTwelveHours;
        DailyIntervalRadioButton.IsChecked =
            settings.AutomaticCheckInterval == AutomaticCheckInterval.Daily;
        WeeklyIntervalRadioButton.IsChecked =
            settings.AutomaticCheckInterval == AutomaticCheckInterval.Weekly;

        _isSynchronizingChoices = false;
    }

    private void LightThemeRadioButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        ApplyThemeChoice(AppTheme.Light);
    }

    private void DarkThemeRadioButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        ApplyThemeChoice(AppTheme.Dark);
    }

    private void AutomaticChecksCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizingChoices)
        {
            return;
        }

        App.CurrentApp.UserSettings.SetAutomaticChecksEnabled(
            AutomaticChecksCheckBox.IsChecked == true
        );
        RefreshChoices();
    }

    private void RunInBackgroundCheckBox_Changed(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizingChoices)
        {
            return;
        }

        App.CurrentApp.UserSettings.SetRunInBackground(
            RunInBackgroundCheckBox.IsChecked == true
        );
        RefreshChoices();
    }

    private void IntervalRadioButton_Checked(
        object sender,
        RoutedEventArgs e)
    {
        if (_isSynchronizingChoices ||
            sender is not System.Windows.Controls.RadioButton
            { Tag: string intervalName } ||
            !Enum.TryParse(
                intervalName,
                ignoreCase: false,
                out AutomaticCheckInterval interval
            ))
        {
            return;
        }

        App.CurrentApp.UserSettings.SetAutomaticCheckInterval(interval);
    }

    private void ApplyThemeChoice(AppTheme theme)
    {
        if (!_isSynchronizingChoices)
        {
            ThemeManager.SetTheme(theme);
        }
    }

    private void OpenApplicationFolderButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        try
        {
            if (!Directory.Exists(ApplicationFolderPath))
            {
                throw new DirectoryNotFoundException();
            }

            _ = Process.Start(new ProcessStartInfo
            {
                FileName = ApplicationFolderPath,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
            when (exception is Win32Exception or
                  InvalidOperationException or
                  IOException or
                  UnauthorizedAccessException)
        {
            System.Windows.MessageBox.Show(
                Window.GetWindow(this),
                "Windows could not open the application folder. " +
                "You can copy the path from the field and open it manually.",
                "Unable to open folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning
            );
        }
    }
}
