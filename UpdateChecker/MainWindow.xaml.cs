using System.Windows;
using System.Windows.Media;
using UpdateChecker.Models;
using UpdateChecker.Services;

namespace UpdateChecker;

public partial class MainWindow : Window
{
    private enum UpdateCheckState
    {
        Ready,
        Checking,
        Cancelling,
        UpdatesFound,
        NoUpdates,
        Cancelled,
        TimedOut,
        Error
    }

    private readonly WingetService _wingetService = new();
    private CancellationTokenSource? _updateCheckCancellation;
    private bool _cancelRequestedByUser;

    private IReadOnlyList<AppUpdateInfo> _updates =
        Array.Empty<AppUpdateInfo>();

    public MainWindow()
    {
        InitializeComponent();

        UpdatesDataGrid.ItemsSource = _updates;
        ApplyState(UpdateCheckState.Ready);
    }

    private async void CheckUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_updateCheckCancellation is { } activeCancellation)
        {
            _cancelRequestedByUser = true;
            ApplyState(UpdateCheckState.Cancelling);
            activeCancellation.Cancel();
            return;
        }

        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromSeconds(60)
        );

        _updateCheckCancellation = cancellation;
        _cancelRequestedByUser = false;

        _updates = Array.Empty<AppUpdateInfo>();
        UpdatesDataGrid.ItemsSource = _updates;
        ApplyState(UpdateCheckState.Checking);

        try
        {
            IReadOnlyList<AppUpdateInfo> updates =
                await _wingetService.GetAvailableUpdatesAsync(
                    cancellation.Token
                );

            _updates = updates;
            UpdatesDataGrid.ItemsSource = _updates;

            ApplyState(
                updates.Count == 0
                    ? UpdateCheckState.NoUpdates
                    : UpdateCheckState.UpdatesFound,
                updateCount: updates.Count
            );
        }
        catch (OperationCanceledException)
        {
            ApplyState(
                _cancelRequestedByUser
                    ? UpdateCheckState.Cancelled
                    : UpdateCheckState.TimedOut
            );
        }
        catch (WingetUnavailableException)
        {
            ApplyState(
                UpdateCheckState.Error,
                "WinGet is not available on this PC."
            );

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
            ApplyState(
                UpdateCheckState.Error,
                "Windows blocked access to WinGet."
            );

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
            ApplyState(
                UpdateCheckState.Error,
                "WinGet could not complete the update check."
            );

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
        catch (WingetOutputParseException)
        {
            ApplyState(
                UpdateCheckState.Error,
                "The WinGet response could not be read."
            );

            ShowUpdateCheckMessage(
                "WinGet returned information in a format this version of " +
                "the app does not recognize. Update WinGet and this app, " +
                "then try again.",
                "WinGet response could not be read",
                MessageBoxImage.Warning
            );
        }
        catch (Exception exception)
        {
            ApplyState(
                UpdateCheckState.Error,
                "An unexpected error interrupted the update check."
            );

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
        }
    }

    private void ApplyState(
        UpdateCheckState state,
        string? statusMessage = null,
        int updateCount = 0)
    {
        bool isBusy = state is
            UpdateCheckState.Checking or
            UpdateCheckState.Cancelling;

        CheckUpdatesButton.Content = state switch
        {
            UpdateCheckState.Checking => "Cancel",
            UpdateCheckState.Cancelling => "Cancelling...",
            _ => "Check for updates"
        };
        CheckUpdatesButton.IsEnabled = state != UpdateCheckState.Cancelling;

        LoadingSpinner.Visibility = isBusy
            ? Visibility.Visible
            : Visibility.Collapsed;

        EmptyStatePanel.Visibility = !isBusy && _updates.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        (string defaultStatus, string emptyTitle, string emptyDescription, string brushKey) =
            state switch
            {
                UpdateCheckState.Ready => (
                    "Ready to check for updates.",
                    "Nothing to show yet",
                    "Run an update check to find newer app versions.",
                    "StatusReadyBrush"
                ),
                UpdateCheckState.Checking => (
                    "Checking for available updates...",
                    "Checking for updates",
                    "WinGet is scanning your installed applications.",
                    "StatusReadyBrush"
                ),
                UpdateCheckState.Cancelling => (
                    "Cancelling the update check...",
                    "Cancelling",
                    "Waiting for WinGet to close safely.",
                    "StatusNeutralBrush"
                ),
                UpdateCheckState.UpdatesFound => (
                    updateCount == 1
                        ? "1 available update was detected."
                        : $"{updateCount} available updates were detected.",
                    "Updates found",
                    "Review the available versions in the table.",
                    "StatusWarningBrush"
                ),
                UpdateCheckState.NoUpdates => (
                    "No available updates were detected.",
                    "You're up to date",
                    "WinGet did not find any available application updates.",
                    "StatusSuccessBrush"
                ),
                UpdateCheckState.Cancelled => (
                    "Update check cancelled.",
                    "Update check cancelled",
                    "Run another check whenever you're ready.",
                    "StatusNeutralBrush"
                ),
                UpdateCheckState.TimedOut => (
                    "Update check timed out. Please try again.",
                    "The check took too long",
                    "Check your connection, then try the update check again.",
                    "StatusWarningBrush"
                ),
                UpdateCheckState.Error => (
                    "The update check could not be completed.",
                    "Unable to check for updates",
                    "Review the warning, then try the update check again.",
                    "StatusErrorBrush"
                ),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(state),
                    state,
                    null
                )
            };

        StatusTextBlock.Text = statusMessage ?? defaultStatus;
        EmptyStateTitleTextBlock.Text = emptyTitle;
        EmptyStateDescriptionTextBlock.Text = emptyDescription;
        StatusIndicator.Fill = (Brush)FindResource(brushKey);
    }

    protected override void OnClosed(EventArgs e)
    {
        _updateCheckCancellation?.Cancel();
        base.OnClosed(e);
    }

    private void ShowUpdateCheckMessage(
        string message,
        string title,
        MessageBoxImage icon)
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
