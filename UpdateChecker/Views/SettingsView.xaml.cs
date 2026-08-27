using System.Windows;
using System.Windows.Controls;
using UpdateChecker.Services;

namespace UpdateChecker.Views;

public partial class SettingsView : UserControl
{
    private bool _isSynchronizingThemeChoice;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    public void RefreshThemeChoice()
    {
        _isSynchronizingThemeChoice = true;

        LightThemeRadioButton.IsChecked =
            ThemeManager.CurrentTheme == AppTheme.Light;
        DarkThemeRadioButton.IsChecked =
            ThemeManager.CurrentTheme == AppTheme.Dark;

        _isSynchronizingThemeChoice = false;
    }

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshThemeChoice();
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

    private void ApplyThemeChoice(AppTheme theme)
    {
        if (!_isSynchronizingThemeChoice)
        {
            ThemeManager.SetTheme(theme);
        }
    }
}
