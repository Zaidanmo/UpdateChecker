using System.Windows;
using System.Windows.Automation;
using System.Windows.Shapes;
using System.Globalization;
using UpdateChecker.Models;
using UpdateChecker.Services;

namespace UpdateChecker.Views;

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

    private readonly UpdateCheckService _updateCheckService;
    private CancellationTokenSource? _updateCheckCancellation;
    private bool _isTrayCheckActive;
    private bool _isSettingsOpen;

    private IReadOnlyList<AppUpdateInfo> _updates =
        Array.Empty<AppUpdateInfo>();

    public MainWindow()
    {
        InitializeComponent();

        _updateCheckService = App.CurrentApp.UpdateCheckService;

        SourceInitialized += MainWindow_SourceInitialized;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;

        UpdatesDataGrid.ItemsSource = _updates;
        ShowSettings(show: false);
        ApplyState(UpdateCheckState.Ready);
    }

    private void SettingsNavigationButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        ShowSettings(!_isSettingsOpen);
    }

    private void MainWindow_SourceInitialized(
        object? sender,
        EventArgs e)
    {
        ApplyTitleBarTheme(IsActive);
    }

    private void MainWindow_Activated(object? sender, EventArgs e)
    {
        ApplyTitleBarTheme(isActive: true);
    }

    private void MainWindow_Deactivated(object? sender, EventArgs e)
    {
        ApplyTitleBarTheme(isActive: false);
    }

    private void ThemeManager_ThemeChanged(AppTheme _)
    {
        ApplyTitleBarTheme(IsActive);
    }

    private void ApplyTitleBarTheme(bool isActive)
    {
        WindowTitleBarService.Apply(
            this,
            ThemeManager.CurrentTheme,
            isActive
        );
    }

    private async void CheckUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_updateCheckCancellation is { } activeCancellation)
        {
            ApplyState(UpdateCheckState.Cancelling);
            activeCancellation.Cancel();
            return;
        }

        using var cancellation = new CancellationTokenSource();

        _updateCheckCancellation = cancellation;

        _updates = Array.Empty<AppUpdateInfo>();
        UpdatesDataGrid.ItemsSource = _updates;
        ApplyState(UpdateCheckState.Checking);

        try
        {
            IReadOnlyList<AppUpdateInfo> updates =
                await _updateCheckService.CheckAsync(
                    cancellation.Token
                );

            App.CurrentApp.UserSettings.RecordSuccessfulCheck(
                DateTimeOffset.UtcNow
            );
            ApplyUpdateResults(updates);
        }
        catch (UpdateCheckTimedOutException)
        {
            ApplyState(UpdateCheckState.TimedOut);
        }
        catch (OperationCanceledException)
        {
            ApplyState(UpdateCheckState.Cancelled);
        }
        catch (UpdateCheckInProgressException)
        {
            ApplyState(
                UpdateCheckState.Ready,
                "An automatic update check is already running."
            );

            ShowUpdateCheckMessage(
                "App Update Checker is already scanning your applications " +
                "in the background. Please wait for it to finish.",
                "Update check already running",
                MessageBoxImage.Information
            );
        }
        catch (Exception exception)
        {
            UpdateCheckFailure failure =
                UpdateCheckErrorMapper.FromException(exception);

            ApplyState(
                UpdateCheckState.Error,
                failure.StatusMessage
            );

            ShowUpdateCheckMessage(
                failure.Message,
                failure.Title,
                failure.IsUnexpected
                    ? MessageBoxImage.Error
                    : MessageBoxImage.Warning
            );
        }
        finally
        {
            _updateCheckCancellation = null;
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
            UpdateCheckState.Checking when _isTrayCheckActive =>
                "Checking...",
            UpdateCheckState.Checking => "Cancel",
            UpdateCheckState.Cancelling => "Cancelling...",
            _ => "Check for updates"
        };
        CheckUpdatesButton.IsEnabled =
            state != UpdateCheckState.Cancelling &&
            !_isTrayCheckActive;
        SettingsNavigationButton.IsEnabled = !isBusy;

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
                    FormatLastSuccessfulCheck(
                        App.CurrentApp.UserSettings.Current
                            .LastSuccessfulCheckUtc
                    ),
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
        StatusIndicator.SetResourceReference(Shape.FillProperty, brushKey);
    }

    private void ShowSettings(bool show)
    {
        _isSettingsOpen = show;

        UpdatesPagePanel.Visibility = show
            ? Visibility.Collapsed
            : Visibility.Visible;
        SettingsPage.Visibility = show
            ? Visibility.Visible
            : Visibility.Collapsed;
        CheckUpdatesButton.Visibility = show
            ? Visibility.Collapsed
            : Visibility.Visible;

        SettingsNavigationButton.Margin = show
            ? new Thickness(0)
            : new Thickness(0, 0, 12, 0);
        SettingsIconPath.Visibility = show
            ? Visibility.Collapsed
            : Visibility.Visible;
        BackIconPath.Visibility = show
            ? Visibility.Visible
            : Visibility.Collapsed;

        string navigationDescription = show
            ? "Return to updates"
            : "Open settings";
        SettingsNavigationButton.ToolTip = navigationDescription;

        AutomationProperties.SetName(
            SettingsNavigationButton,
            navigationDescription
        );

        if (show)
        {
            SettingsPage.RefreshChoices();
        }
    }

    internal void ApplyBackgroundCheckResult(
        IReadOnlyList<AppUpdateInfo> updates)
    {
        if (_updateCheckCancellation is not null)
        {
            return;
        }

        ApplyUpdateResults(updates);
    }

    internal void BeginTrayUpdateCheck()
    {
        if (_updateCheckCancellation is not null || _isTrayCheckActive)
        {
            return;
        }

        _isTrayCheckActive = true;
        _updates = Array.Empty<AppUpdateInfo>();
        UpdatesDataGrid.ItemsSource = _updates;
        ShowSettings(show: false);
        ApplyState(UpdateCheckState.Checking);
    }

    internal void CompleteTrayUpdateCheck(TrayUpdateCheckResult result)
    {
        if (!_isTrayCheckActive)
        {
            return;
        }

        _isTrayCheckActive = false;

        switch (result.Status)
        {
            case TrayUpdateCheckStatus.Succeeded:
                ApplyUpdateResults(
                    result.Updates ?? Array.Empty<AppUpdateInfo>()
                );
                break;
            case TrayUpdateCheckStatus.Busy:
                ApplyState(
                    UpdateCheckState.Ready,
                    "Another update check is already running."
                );
                break;
            case TrayUpdateCheckStatus.TimedOut:
                ApplyState(UpdateCheckState.TimedOut);
                break;
            case TrayUpdateCheckStatus.Cancelled:
                ApplyState(UpdateCheckState.Cancelled);
                break;
            case TrayUpdateCheckStatus.Failed:
                ApplyState(
                    UpdateCheckState.Error,
                    result.Failure?.StatusMessage
                );
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(result),
                    result.Status,
                    null
                );
        }
    }

    internal void CancelActiveCheck()
    {
        _updateCheckCancellation?.Cancel();
    }

    private void ApplyUpdateResults(IReadOnlyList<AppUpdateInfo> updates)
    {
        _updates = updates;
        UpdatesDataGrid.ItemsSource = _updates;
        ApplyState(
            updates.Count == 0
                ? UpdateCheckState.NoUpdates
                : UpdateCheckState.UpdatesFound,
            updateCount: updates.Count
        );
    }

    protected override void OnClosed(EventArgs e)
    {
        Activated -= MainWindow_Activated;
        Deactivated -= MainWindow_Deactivated;
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        _updateCheckCancellation?.Cancel();
        base.OnClosed(e);
    }

    private void ShowUpdateCheckMessage(
        string message,
        string title,
        MessageBoxImage icon)
    {
        System.Windows.MessageBox.Show(
            this,
            message,
            title,
            MessageBoxButton.OK,
            icon
        );
    }

    internal static string FormatLastSuccessfulCheck(
        DateTimeOffset? checkedAtUtc,
        CultureInfo? culture = null)
    {
        if (checkedAtUtc is null)
        {
            return "Last checked: Never";
        }

        string formatted = checkedAtUtc.Value
            .ToLocalTime()
            .ToString("g", culture ?? CultureInfo.CurrentCulture);
        return $"Last checked: {formatted}";
    }

}
