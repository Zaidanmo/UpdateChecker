using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using UpdateChecker.Models;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingIcon = System.Drawing.Icon;
using Forms = System.Windows.Forms;

namespace UpdateChecker.Services;

internal sealed class TrayIconService : IUpdateNotificationSink, IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ContextMenuStrip _contextMenu;
    private readonly DrawingIcon _icon;
    private readonly DrawingFont _menuFont;
    private readonly DrawingFont _menuBoldFont;
    private bool _isDarkMenu;
    private bool _disposed;

    public TrayIconService(
        Action openApplication,
        Func<Task> checkForUpdates,
        Func<Task> exitApplication)
    {
        _icon = LoadApplicationIcon();
        _menuFont = new DrawingFont(
            Forms.Control.DefaultFont.FontFamily,
            10F,
            System.Drawing.FontStyle.Regular
        );
        _menuBoldFont = new DrawingFont(
            Forms.Control.DefaultFont.FontFamily,
            10F,
            System.Drawing.FontStyle.Bold
        );

        _contextMenu = new Forms.ContextMenuStrip
        {
            AutoSize = true,
            DropShadowEnabled = true,
            MinimumSize = new System.Drawing.Size(238, 0),
            Padding = new Forms.Padding(6),
            ShowCheckMargin = false,
            ShowImageMargin = false
        };
        _contextMenu.Opening += ContextMenu_Opening;
        _contextMenu.Opened += ContextMenu_Opened;

        Forms.ToolStripItem openItem = _contextMenu.Items.Add(
            "Open App Update Checker"
        );
        ConfigureMenuItem(openItem, _menuBoldFont);
        openItem.Click += (_, _) => openApplication();

        Forms.ToolStripItem checkItem = _contextMenu.Items.Add(
            "Check for updates now",
            image: null,
            (_, _) => _ = checkForUpdates()
        );
        ConfigureMenuItem(checkItem, _menuFont);

        var separator = new Forms.ToolStripSeparator
        {
            Margin = new Forms.Padding(8, 5, 8, 5)
        };
        _contextMenu.Items.Add(separator);

        Forms.ToolStripItem exitItem = _contextMenu.Items.Add(
            "Exit",
            image: null,
            (_, _) => _ = exitApplication()
        );
        ConfigureMenuItem(exitItem, _menuFont);
        ApplyMenuTheme();

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = CreateStatusText(TrayIconStatus.Ready),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        _notifyIcon.DoubleClick += (_, _) => openApplication();
        _notifyIcon.BalloonTipClicked += (_, _) => openApplication();
    }

    public void SetStatus(TrayIconStatus status, int updateCount = 0)
    {
        InvokeOnApplicationThread(() =>
        {
            if (!_disposed)
            {
                _notifyIcon.Text = CreateStatusText(status, updateCount);
            }
        });
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
        _contextMenu.Opening -= ContextMenu_Opening;
        _contextMenu.Opened -= ContextMenu_Opened;
        _contextMenu.Dispose();
        _notifyIcon.Dispose();
        _menuBoldFont.Dispose();
        _menuFont.Dispose();
        _icon.Dispose();
    }

    private void Show(
        string title,
        string message,
        Forms.ToolTipIcon icon)
    {
        InvokeOnApplicationThread(() =>
        {
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
        });
    }

    private static string CreateStatusText(
        TrayIconStatus status,
        int updateCount = 0)
    {
        return status switch
        {
            TrayIconStatus.Checking =>
                "App Update Checker - Checking for updates...",
            TrayIconStatus.UpToDate =>
                "App Update Checker - Applications are up to date",
            TrayIconStatus.UpdatesAvailable when updateCount == 1 =>
                "App Update Checker - 1 update available",
            TrayIconStatus.UpdatesAvailable =>
                $"App Update Checker - {updateCount} updates available",
            TrayIconStatus.Failed =>
                "App Update Checker - Last check failed",
            _ => "App Update Checker - Ready"
        };
    }

    private void ContextMenu_Opening(object? sender, CancelEventArgs e)
    {
        ApplyMenuTheme();
    }

    private void ContextMenu_Opened(object? sender, EventArgs e)
    {
        ApplyNativeMenuAppearance(_contextMenu.Handle, _isDarkMenu);
    }

    private void ApplyMenuTheme()
    {
        _isDarkMenu =
            SystemThemeService.GetPreferredAppTheme() == AppTheme.Dark;
        TrayMenuPalette palette = TrayMenuPalette.Create(_isDarkMenu);

        _contextMenu.BackColor = palette.Background;
        _contextMenu.ForeColor = palette.Text;
        _contextMenu.Renderer = new Forms.ToolStripProfessionalRenderer(
            new ModernTrayColorTable(palette)
        )
        {
            RoundedEdges = true
        };

        foreach (Forms.ToolStripItem item in _contextMenu.Items)
        {
            item.BackColor = palette.Background;
            item.ForeColor = palette.Text;
        }
    }

    private static void ConfigureMenuItem(
        Forms.ToolStripItem item,
        DrawingFont font)
    {
        item.AutoSize = true;
        item.Font = font;
        item.Margin = Forms.Padding.Empty;
        item.Padding = new Forms.Padding(12, 8, 12, 8);
    }

    private static void ApplyNativeMenuAppearance(
        IntPtr windowHandle,
        bool darkMode)
    {
        int useDarkMode = darkMode ? 1 : 0;
        int roundCorners = 2;

        _ = DwmSetWindowAttribute(
            windowHandle,
            20,
            ref useDarkMode,
            Marshal.SizeOf<int>()
        );
        _ = DwmSetWindowAttribute(
            windowHandle,
            33,
            ref roundCorners,
            Marshal.SizeOf<int>()
        );
        _ = SetWindowTheme(
            windowHandle,
            darkMode ? "DarkMode_Explorer" : "Explorer",
            null
        );
    }

    private static void InvokeOnApplicationThread(Action action)
    {
        System.Windows.Application? application =
            System.Windows.Application.Current;

        if (application is not null &&
            !application.Dispatcher.CheckAccess())
        {
            _ = application.Dispatcher.InvokeAsync(action);
            return;
        }

        action();
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

    private readonly record struct TrayMenuPalette(
        DrawingColor Background,
        DrawingColor Text,
        DrawingColor Hover,
        DrawingColor Pressed,
        DrawingColor Border,
        DrawingColor Separator)
    {
        public static TrayMenuPalette Create(bool darkMode)
        {
            if (Forms.SystemInformation.HighContrast)
            {
                return new TrayMenuPalette(
                    System.Drawing.SystemColors.Menu,
                    System.Drawing.SystemColors.MenuText,
                    System.Drawing.SystemColors.Highlight,
                    System.Drawing.SystemColors.Highlight,
                    System.Drawing.SystemColors.WindowFrame,
                    System.Drawing.SystemColors.GrayText
                );
            }

            return darkMode
                ? new TrayMenuPalette(
                    DrawingColor.FromArgb(31, 31, 31),
                    DrawingColor.FromArgb(245, 245, 245),
                    DrawingColor.FromArgb(51, 51, 51),
                    DrawingColor.FromArgb(61, 61, 61),
                    DrawingColor.FromArgb(66, 66, 66),
                    DrawingColor.FromArgb(73, 73, 73)
                )
                : new TrayMenuPalette(
                    DrawingColor.FromArgb(255, 255, 255),
                    DrawingColor.FromArgb(28, 31, 36),
                    DrawingColor.FromArgb(241, 244, 248),
                    DrawingColor.FromArgb(229, 234, 241),
                    DrawingColor.FromArgb(210, 216, 224),
                    DrawingColor.FromArgb(222, 226, 232)
                );
        }
    }

    private sealed class ModernTrayColorTable :
        Forms.ProfessionalColorTable
    {
        private readonly TrayMenuPalette _palette;

        public ModernTrayColorTable(TrayMenuPalette palette)
        {
            _palette = palette;
            UseSystemColors = false;
        }

        public override DrawingColor ToolStripDropDownBackground =>
            _palette.Background;

        public override DrawingColor MenuBorder => _palette.Border;

        public override DrawingColor MenuItemBorder => _palette.Hover;

        public override DrawingColor MenuItemSelected => _palette.Hover;

        public override DrawingColor MenuItemSelectedGradientBegin =>
            _palette.Hover;

        public override DrawingColor MenuItemSelectedGradientEnd =>
            _palette.Hover;

        public override DrawingColor MenuItemPressedGradientBegin =>
            _palette.Pressed;

        public override DrawingColor MenuItemPressedGradientEnd =>
            _palette.Pressed;

        public override DrawingColor SeparatorDark => _palette.Separator;

        public override DrawingColor SeparatorLight => _palette.Separator;

        public override DrawingColor ImageMarginGradientBegin =>
            _palette.Background;

        public override DrawingColor ImageMarginGradientMiddle =>
            _palette.Background;

        public override DrawingColor ImageMarginGradientEnd =>
            _palette.Background;
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize);

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(
        IntPtr windowHandle,
        string? subAppName,
        string? subIdList);
}
