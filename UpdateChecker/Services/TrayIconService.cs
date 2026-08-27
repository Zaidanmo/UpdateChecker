using System.IO;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace UpdateChecker.Services;

internal sealed class TrayIconService : IUpdateNotificationSink, IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly DrawingIcon _icon;
    private bool _disposed;

    public TrayIconService(
        Action openApplication,
        Func<Task> checkForUpdates,
        Func<Task> exitApplication)
    {
        _icon = LoadApplicationIcon();

        var contextMenu = new Forms.ContextMenuStrip();
        Forms.ToolStripItem openItem = contextMenu.Items.Add(
            "Open App Update Checker"
        );
        openItem.Font = new System.Drawing.Font(
            openItem.Font,
            System.Drawing.FontStyle.Bold
        );
        openItem.Click += (_, _) => openApplication();

        contextMenu.Items.Add(
            "Check for updates now",
            image: null,
            (_, _) => _ = checkForUpdates()
        );
        contextMenu.Items.Add(new Forms.ToolStripSeparator());
        contextMenu.Items.Add(
            "Exit",
            image: null,
            (_, _) => _ = exitApplication()
        );

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "App Update Checker",
            ContextMenuStrip = contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => openApplication();
        _notifyIcon.BalloonTipClicked += (_, _) => openApplication();
    }

    public void ShowUpdatesFound(int updateCount, int majorUpdateCount)
    {
        string title = updateCount == 1
            ? "1 application update is available"
            : $"{updateCount} application updates are available";

        string message = majorUpdateCount switch
        {
            0 => "Open App Update Checker to review the available versions.",
            1 => "1 is a major update. Open the app to review it.",
            _ => $"{majorUpdateCount} are major updates. Open the app to review them."
        };

        Show(title, message, Forms.ToolTipIcon.Info);
    }

    public void ShowInformation(string title, string message)
    {
        Show(title, message, Forms.ToolTipIcon.Info);
    }

    public void ShowWarning(string title, string message)
    {
        Show(title, message, Forms.ToolTipIcon.Warning);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    private void Show(
        string title,
        string message,
        Forms.ToolTipIcon icon)
    {
        System.Windows.Application? application =
            System.Windows.Application.Current;

        if (application is not null &&
            !application.Dispatcher.CheckAccess())
        {
            _ = application.Dispatcher.InvokeAsync(
                () => Show(title, message, icon)
            );
            return;
        }

        if (_disposed)
        {
            return;
        }

        _notifyIcon.ShowBalloonTip(
            timeout: 5000,
            tipTitle: title,
            tipText: message,
            tipIcon: icon
        );
    }

    private static DrawingIcon LoadApplicationIcon()
    {
        const string iconResourceUri =
            "pack://application:,,,/UpdateChecker;component/Assets/app_logo.ico";

        System.Windows.Resources.StreamResourceInfo? resource =
            System.Windows.Application.GetResourceStream(
                new Uri(iconResourceUri, UriKind.Absolute)
            );

        if (resource is not null)
        {
            using Stream stream = resource.Stream;
            using var resourceIcon = new DrawingIcon(stream);
            return (DrawingIcon)resourceIcon.Clone();
        }

        return (DrawingIcon)System.Drawing.SystemIcons.Application.Clone();
    }
}
