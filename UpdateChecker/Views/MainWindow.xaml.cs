using System.Windows;
using System.ComponentModel;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Runtime.InteropServices;
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
    private readonly DispatcherTimer _relativeTimeRefreshTimer;
    private readonly List<SortDescription> _updateSortDescriptions = [];
    private CancellationTokenSource? _updateCheckCancellation;
    private bool _isTrayCheckActive;
    private bool _isSettingsOpen;
    private UpdateCheckState _currentState;

    private IReadOnlyList<AppUpdateInfo> _updates =
        Array.Empty<AppUpdateInfo>();

    public MainWindow()
    {
        InitializeComponent();

        _updateCheckService = App.CurrentApp.UpdateCheckService;
        _relativeTimeRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _relativeTimeRefreshTimer.Tick +=
            RelativeTimeRefreshTimer_Tick;

        SourceInitialized += MainWindow_SourceInitialized;
        IsVisibleChanged += MainWindow_IsVisibleChanged;
        Activated += MainWindow_Activated;
        Deactivated += MainWindow_Deactivated;
        ThemeManager.ThemeChanged += ThemeManager_ThemeChanged;

        UpdateDataGridItemsSource();
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

    private void CheckUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        _ = RunManualCheckAsync();
    }

    private async Task RunManualCheckAsync()
    {
        if (_updateCheckCancellation is { } activeCancellation)
        {
            ApplyState(UpdateCheckState.Cancelling);
            activeCancellation.Cancel();
            return;
        }

        using var cancellation = new CancellationTokenSource();

        _updateCheckCancellation = cancellation;
        HideInlineError();

        SetUpdates(Array.Empty<AppUpdateInfo>());
        ApplyState(UpdateCheckState.Checking);
        App.CurrentApp.SetTrayStatus(TrayIconStatus.Checking);

        try
        {
            IReadOnlyList<AppUpdateInfo> updates =
                await _updateCheckService.CheckAsync(
                    cancellation.Token
                );

            App.CurrentApp.UserSettings.RecordSuccessfulCheck(
                DateTimeOffset.UtcNow
            );
            App.CurrentApp.SetTrayStatus(
                updates.Count == 0
                    ? TrayIconStatus.UpToDate
                    : TrayIconStatus.UpdatesAvailable,
                updates.Count
            );
            ApplyUpdateResults(updates);
        }
        catch (UpdateCheckTimedOutException)
        {
            App.CurrentApp.SetTrayStatus(TrayIconStatus.Failed);
            ApplyState(UpdateCheckState.TimedOut);
            ShowInlineError(
                "Update check timed out",
                "WinGet took too long to respond. Check your connection " +
                "and try again."
            );
        }
        catch (OperationCanceledException)
        {
            App.CurrentApp.SetTrayStatus(TrayIconStatus.Ready);
            ApplyState(UpdateCheckState.Cancelled);
        }
        catch (UpdateCheckInProgressException)
        {
            ApplyState(
                UpdateCheckState.Ready,
                "An automatic update check is already running."
            );

            ShowInlineError(
                "Update check already running",
                "App Update Checker is already scanning your applications " +
                "in the background. Please wait for it to finish."
            );
        }
        catch (Exception exception)
        {
            App.CurrentApp.SetTrayStatus(TrayIconStatus.Failed);
            UpdateCheckFailure failure =
                UpdateCheckErrorMapper.FromException(exception);

            ApplyState(
                UpdateCheckState.Error,
                failure.StatusMessage
            );

            ShowInlineError(
                failure.Title,
                failure.Message
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
        _currentState = state;
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
                            .LastSuccessfulCheckUtc,
                        DateTimeOffset.UtcNow
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
        StatusTextBlock.ToolTip = CreateLastCheckToolTip(state);
        EmptyStateTitleTextBlock.Text = emptyTitle;
        EmptyStateDescriptionTextBlock.Text = emptyDescription;
        StatusIndicator.SetResourceReference(Shape.FillProperty, brushKey);
    }

    private void ShowSettings(bool show)
    {
        if (!show)
        {
            SettingsPage.ClearTransientFeedback();
        }

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
            ? "Return to updates (Esc)"
            : "Open settings (Ctrl+,)";
        SettingsNavigationButton.ToolTip = navigationDescription;

        AutomationProperties.SetName(
            SettingsNavigationButton,
            navigationDescription
        );

        if (show)
        {
            SettingsPage.RefreshChoices();

            if (IsLoaded)
            {
                _ = Dispatcher.BeginInvoke(
                    SettingsPage.FocusFirstControl
                );
            }
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
        SetUpdates(Array.Empty<AppUpdateInfo>());
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
                ShowInlineError(
                    "Update check timed out",
                    "WinGet took too long to respond. Check your " +
                    "connection and try again."
                );
                break;
            case TrayUpdateCheckStatus.Cancelled:
                ApplyState(UpdateCheckState.Cancelled);
                break;
            case TrayUpdateCheckStatus.Failed:
                ApplyState(
                    UpdateCheckState.Error,
                    result.Failure?.StatusMessage
                );
                ShowInlineError(
                    result.Failure?.Title ?? "Unable to check for updates",
                    result.Failure?.Message ??
                    "The update check could not be completed."
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
        HideInlineError();
        SetUpdates(updates);
        ApplyState(
            updates.Count == 0
                ? UpdateCheckState.NoUpdates
                : UpdateCheckState.UpdatesFound,
            updateCount: updates.Count
        );
    }

    protected override void OnClosed(EventArgs e)
    {
        _relativeTimeRefreshTimer.Stop();
        _relativeTimeRefreshTimer.Tick -=
            RelativeTimeRefreshTimer_Tick;
        SourceInitialized -= MainWindow_SourceInitialized;
        IsVisibleChanged -= MainWindow_IsVisibleChanged;
        Activated -= MainWindow_Activated;
        Deactivated -= MainWindow_Deactivated;
        ThemeManager.ThemeChanged -= ThemeManager_ThemeChanged;
        _updateCheckCancellation?.Cancel();
        DetachUpdateDataGridItemsSource();
        _updates = Array.Empty<AppUpdateInfo>();
        base.OnClosed(e);
    }

    private void MainWindow_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            UpdateDataGridItemsSource();
            RelativeTimeRefreshTimer_Tick(this, EventArgs.Empty);
            _relativeTimeRefreshTimer.Start();
            return;
        }

        _relativeTimeRefreshTimer.Stop();
        DetachUpdateDataGridItemsSource();
    }

    private void SetUpdates(IReadOnlyList<AppUpdateInfo> updates)
    {
        CaptureUpdateSortDescriptions();
        _updates = updates;
        UpdateDataGridItemsSource();
    }

    private void UpdateDataGridItemsSource()
    {
        if (!IsVisible)
        {
            DetachUpdateDataGridItemsSource();
            return;
        }

        if (ReferenceEquals(UpdatesDataGrid.ItemsSource, _updates))
        {
            return;
        }

        UpdatesDataGrid.ItemsSource = _updates;

        foreach (SortDescription sort in _updateSortDescriptions)
        {
            UpdatesDataGrid.Items.SortDescriptions.Add(sort);
        }
    }

    private void DetachUpdateDataGridItemsSource()
    {
        CaptureUpdateSortDescriptions();
        UpdatesDataGrid.ItemsSource = null;
    }

    private void CaptureUpdateSortDescriptions()
    {
        if (UpdatesDataGrid.ItemsSource is null)
        {
            return;
        }

        _updateSortDescriptions.Clear();

        foreach (SortDescription sort in
                 UpdatesDataGrid.Items.SortDescriptions)
        {
            _updateSortDescriptions.Add(sort);
        }
    }

    private void RelativeTimeRefreshTimer_Tick(
        object? sender,
        EventArgs e)
    {
        if (_currentState == UpdateCheckState.Ready &&
            InlineErrorPanel.Visibility != Visibility.Visible)
        {
            ApplyState(UpdateCheckState.Ready);
        }

        if (_isSettingsOpen)
        {
            SettingsPage.RefreshTimeSummaries();
        }
    }

    private void RetryUpdateCheckButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        HideInlineError();
        _ = RunManualCheckAsync();
    }

    private void DismissInlineErrorButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        HideInlineError();
    }

    private void ShowInlineError(string title, string message)
    {
        InlineErrorTitleTextBlock.Text = title;
        InlineErrorMessageTextBlock.Text = message;
        InlineErrorPanel.Visibility = Visibility.Visible;
    }

    private void HideInlineError()
    {
        InlineErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void CopyUpgradeCommandButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button
            { Tag: AppUpdateInfo update })
        {
            return;
        }

        string command = CreateUpgradeCommand(update);

        try
        {
            System.Windows.Clipboard.SetText(command);
            HideInlineError();
            StatusTextBlock.Text =
                $"Upgrade command copied for {update.Name}.";
            StatusTextBlock.ToolTip = command;
            StatusIndicator.SetResourceReference(
                Shape.FillProperty,
                "StatusSuccessBrush"
            );
        }
        catch (ExternalException)
        {
            ShowInlineError(
                "Unable to copy command",
                "Windows could not access the clipboard. Try again."
            );
        }
    }

    private void MainWindow_PreviewKeyDown(
        object sender,
        System.Windows.Input.KeyEventArgs e)
    {
        bool controlPressed =
            Keyboard.Modifiers.HasFlag(ModifierKeys.Control);

        if (e.Key == Key.Escape)
        {
            if (_isSettingsOpen)
            {
                ShowSettings(show: false);
                SettingsNavigationButton.Focus();
                e.Handled = true;
            }
            else if (InlineErrorPanel.Visibility == Visibility.Visible)
            {
                HideInlineError();
                e.Handled = true;
            }

            return;
        }

        if (controlPressed && e.Key == Key.OemComma)
        {
            if (SettingsNavigationButton.IsEnabled)
            {
                ShowSettings(!_isSettingsOpen);
                e.Handled = true;
            }

            return;
        }

        if ((e.Key == Key.F5 ||
             (controlPressed && e.Key == Key.R)) &&
            _updateCheckCancellation is null &&
            !_isTrayCheckActive)
        {
            ShowSettings(show: false);
            _ = RunManualCheckAsync();
            e.Handled = true;
        }
    }

    internal static string CreateUpgradeCommand(AppUpdateInfo update)
    {
        string safeId = new(
            update.Id
                .Where(character =>
                    !char.IsControl(character) && character != '"')
                .ToArray()
        );

        return
            $"winget upgrade --id \"{safeId}\" --exact " +
            "--accept-source-agreements --accept-package-agreements";
    }

    internal static string FormatLastSuccessfulCheck(
        DateTimeOffset? checkedAtUtc,
        DateTimeOffset now)
    {
        if (checkedAtUtc is null)
        {
            return "Last checked: Never";
        }

        return "Last checked: " + DateTimeDisplayFormatter.FormatRelative(
            checkedAtUtc.Value,
            now
        );
    }

    private string? CreateLastCheckToolTip(UpdateCheckState state)
    {
        DateTimeOffset? lastCheck = App.CurrentApp.UserSettings.Current
            .LastSuccessfulCheckUtc;

        return state == UpdateCheckState.Ready && lastCheck is not null
            ? "Last successful check: " +
              DateTimeDisplayFormatter.FormatExact(lastCheck.Value)
            : null;
    }

}
