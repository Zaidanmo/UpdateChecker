using System.Collections.ObjectModel;
using System.Windows;
using UpdateChecker.Models;
using UpdateChecker.Services;

namespace UpdateChecker;

public partial class MainWindow : Window
{
    private readonly WingetService _wingetService = new();

    private readonly ObservableCollection<AppUpdateInfo> _updates =
        new();

    public MainWindow()
    {
        InitializeComponent();

        UpdatesDataGrid.ItemsSource = _updates;
    }

    private async void CheckUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        SetLoadingState(true);

        _updates.Clear();
        StatusTextBlock.Text = "Checking for available updates...";

        try
        {
            IReadOnlyList<AppUpdateInfo> updates =
                await _wingetService.GetAvailableUpdatesAsync();

            foreach (AppUpdateInfo update in updates)
            {
                _updates.Add(update);
            }

            StatusTextBlock.Text = updates.Count switch
            {
                0 => "No available updates were detected.",
                1 => "1 available update was detected.",
                _ => $"{updates.Count} available updates were detected."
            };
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "The update check failed.";

            MessageBox.Show(
                this,
                exception.Message,
                "Update check failed",
                MessageBoxButton.OK,
                MessageBoxImage.Error
            );
        }
        finally
        {
            SetLoadingState(false);
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        CheckUpdatesButton.IsEnabled = !isLoading;

        LoadingProgressBar.Visibility = isLoading
            ? Visibility.Visible
            : Visibility.Collapsed;

        EmptyStatePanel.Visibility = !isLoading && _updates.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}
