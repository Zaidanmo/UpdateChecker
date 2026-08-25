using System.Windows;
using UpdateChecker.Models;
using UpdateChecker.Services;

namespace UpdateChecker;

public partial class MainWindow : Window
{
    private readonly WingetService _wingetService = new();
    private CancellationTokenSource? _updateCheckCancellation;
    private bool _cancelRequestedByUser;

    private IReadOnlyList<AppUpdateInfo> _updates =
        Array.Empty<AppUpdateInfo>();

    public MainWindow()
    {
        InitializeComponent();

        UpdatesDataGrid.ItemsSource = _updates;
    }

    private async void CheckUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_updateCheckCancellation is { } activeCancellation)
        {
            _cancelRequestedByUser = true;
            CheckUpdatesButton.Content = "Cancelling...";
            CheckUpdatesButton.IsEnabled = false;
            activeCancellation.Cancel();
            return;
        }

        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(60)
        );

        _updateCheckCancellation = cancellation;
        _cancelRequestedByUser = false;

        SetLoadingState(true);

        _updates = Array.Empty<AppUpdateInfo>();
        UpdatesDataGrid.ItemsSource = _updates;
        StatusTextBlock.Text = "Checking for available updates...";

        try
        {
            IReadOnlyList<AppUpdateInfo> updates =
                await _wingetService.GetAvailableUpdatesAsync(
                    cancellation.Token
                );

            _updates = updates;
            UpdatesDataGrid.ItemsSource = _updates;

            StatusTextBlock.Text = updates.Count switch
            {
                0 => "No available updates were detected.",
                1 => "1 available update was detected.",
                _ => $"{updates.Count} available updates were detected."
            };
        }
        catch (OperationCanceledException)
        {
            StatusTextBlock.Text = _cancelRequestedByUser
                ? "Update check cancelled."
                : "Update check timed out. Please try again.";
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
            _updateCheckCancellation = null;
            _cancelRequestedByUser = false;
            SetLoadingState(false);
        }
    }

    private void SetLoadingState(bool isLoading)
    {
        CheckUpdatesButton.Content = isLoading
            ? "Cancel"
            : "Check for updates";
        CheckUpdatesButton.IsEnabled = true;

        LoadingProgressBar.Visibility = isLoading
            ? Visibility.Visible
            : Visibility.Collapsed;

        EmptyStatePanel.Visibility = !isLoading && _updates.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateCheckCancellation?.Cancel();
        base.OnClosed(e);
    }
}
