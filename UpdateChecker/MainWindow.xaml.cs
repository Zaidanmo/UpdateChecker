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
        catch (WingetUnavailableException)
        {
            StatusTextBlock.Text = "WinGet is not available on this PC.";

            ShowUpdateCheckMessage(
                "WinGet is required to check for application updates. " +
                "Install Microsoft App Installer from the Microsoft Store, " +
                "then restart this app.",
                "WinGet is not available",
                MessageBoxImage.Warning
            );
        }
        catch (WingetAccessDeniedException)
        {
            StatusTextBlock.Text = "Windows blocked access to WinGet.";

            ShowUpdateCheckMessage(
                "Windows prevented this app from starting WinGet. " +
                "Check your security policy or contact your administrator, " +
                "then try again.",
                "WinGet access was denied",
                MessageBoxImage.Warning
            );
        }
        catch (WingetCommandException exception)
        {
            StatusTextBlock.Text = "WinGet could not complete the update check.";

            string details = GetSafeErrorDetails(exception.Details);
            string message =
                "WinGet could not complete the scan. Check your internet " +
                "connection and WinGet sources, then try again." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Exit code: {exception.ExitCode}";

            if (!string.IsNullOrWhiteSpace(details))
            {
                message +=
                    $"{Environment.NewLine}{Environment.NewLine}" +
                    $"Details: {details}";
            }

            ShowUpdateCheckMessage(
                message,
                "Update check could not be completed",
                MessageBoxImage.Warning
            );
        }
        catch (Exception exception)
        {
            StatusTextBlock.Text = "An unexpected error interrupted the update check.";

            ShowUpdateCheckMessage(
                "An unexpected error occurred while checking for updates. " +
                "Please try again." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Details: {GetSafeErrorDetails(exception.Message)}",
                "Unexpected update-check error",
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

    private void ShowUpdateCheckMessage(string message, string title, MessageBoxImage icon)
    {
        MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.OK,
            icon
        );
    }

    private static string GetSafeErrorDetails(string details)
    {
        const int maximumLength = 500;

        string normalizedDetails = details
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return normalizedDetails.Length <= maximumLength
            ? normalizedDetails
            : $"{normalizedDetails[..maximumLength]}...";
    }
}
