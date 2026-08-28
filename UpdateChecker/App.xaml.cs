using System.ComponentModel;
using System.Windows;
using UpdateChecker.Models;
using UpdateChecker.Services;
using UpdateChecker.Views;

namespace UpdateChecker;

public partial class App : System.Windows.Application
{
    private TrayIconService? _trayIconService;
    private BackgroundUpdateService? _backgroundUpdateService;
    private MainWindow? _mainWindow;
    private bool _isExitRequested;
    private bool _backgroundNoticeShown;
    private Task? _exitTask;

    internal UserSettingsManager UserSettings { get; } = new();

    internal UpdateCheckService UpdateCheckService { get; } = new(
        new WingetService()
    );

    internal static App CurrentApp => (App)Current;

    internal void SetTrayStatus(
        TrayIconStatus status,
        int updateCount = 0)
    {
        _trayIconService?.SetStatus(status, updateCount);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        UserSettings.Initialize();
        ThemeManager.Initialize(UserSettings);
        base.OnStartup(e);

        _mainWindow = new MainWindow();
        MainWindow = _mainWindow;
        _mainWindow.Closing += MainWindow_Closing;

        _trayIconService = new TrayIconService(
            ShowMainWindow,
            () => _backgroundUpdateService?.CheckNowFromTrayAsync() ??
                  Task.CompletedTask,
            RequestExitAsync
        );

        _backgroundUpdateService = new BackgroundUpdateService(
            UpdateCheckService,
            UserSettings,
            _trayIconService
        );
        _backgroundUpdateService.UpdatesChecked +=
            BackgroundUpdateService_UpdatesChecked;
        _backgroundUpdateService.TrayCheckStarted +=
            BackgroundUpdateService_TrayCheckStarted;
        _backgroundUpdateService.TrayCheckCompleted +=
            BackgroundUpdateService_TrayCheckCompleted;
        _backgroundUpdateService.Start();

        _mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_mainWindow is not null)
        {
            _mainWindow.Closing -= MainWindow_Closing;
        }

        if (_backgroundUpdateService is not null)
        {
            _backgroundUpdateService.UpdatesChecked -=
                BackgroundUpdateService_UpdatesChecked;
            _backgroundUpdateService.TrayCheckStarted -=
                BackgroundUpdateService_TrayCheckStarted;
            _backgroundUpdateService.TrayCheckCompleted -=
                BackgroundUpdateService_TrayCheckCompleted;
            _backgroundUpdateService.Dispose();
        }

        _trayIconService?.Dispose();
        ThemeManager.Shutdown();
        base.OnExit(e);
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            return;
        }

        if (UserSettings.Current.RunInBackground)
        {
            e.Cancel = true;
            _mainWindow?.Hide();

            if (!_backgroundNoticeShown)
            {
                _backgroundNoticeShown = true;
                _trayIconService?.ShowInformation(
                    "App Update Checker is still running",
                    "Use the notification-area icon to open or exit the app."
                );
            }

            return;
        }

        e.Cancel = true;
        _ = RequestExitAsync();
    }

    private void ShowMainWindow()
    {
        Dispatcher.Invoke(() =>
        {
            if (_mainWindow is null)
            {
                return;
            }

            if (!_mainWindow.IsVisible)
            {
                _mainWindow.Show();
            }

            if (_mainWindow.WindowState == WindowState.Minimized)
            {
                _mainWindow.WindowState = WindowState.Normal;
            }

            _mainWindow.Activate();
        });
    }

    private Task RequestExitAsync()
    {
        if (!Dispatcher.CheckAccess())
        {
            return Dispatcher
                .InvokeAsync(RequestExitAsync)
                .Task
                .Unwrap();
        }

        return _exitTask ??= ExitCoreAsync();
    }

    private async Task ExitCoreAsync()
    {
        _isExitRequested = true;
        _mainWindow?.CancelActiveCheck();

        if (_backgroundUpdateService is not null)
        {
            await _backgroundUpdateService.StopAsync();
        }

        Shutdown();
    }

    private void BackgroundUpdateService_UpdatesChecked(
        IReadOnlyList<AppUpdateInfo> updates)
    {
        _ = Dispatcher.InvokeAsync(
            () => _mainWindow?.ApplyBackgroundCheckResult(updates)
        );
    }

    private void BackgroundUpdateService_TrayCheckStarted()
    {
        _ = Dispatcher.InvokeAsync(
            () => _mainWindow?.BeginTrayUpdateCheck()
        );
    }

    private void BackgroundUpdateService_TrayCheckCompleted(
        TrayUpdateCheckResult result)
    {
        _ = Dispatcher.InvokeAsync(
            () => _mainWindow?.CompleteTrayUpdateCheck(result)
        );
    }
}
