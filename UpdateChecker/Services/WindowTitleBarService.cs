using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal static class WindowTitleBarService
{
    private const int UseImmersiveDarkMode = 20;
    private const int BorderColor = 34;
    private const int CaptionColor = 35;
    private const int TextColor = 36;

    public static void Apply(
        Window window,
        AppTheme theme,
        bool isActive)
    {
        IntPtr windowHandle = new WindowInteropHelper(window).Handle;

        if (windowHandle == IntPtr.Zero)
        {
            return;
        }

        int useDarkMode = theme == AppTheme.Dark ? 1 : 0;
        _ = DwmSetWindowAttribute(
            windowHandle,
            UseImmersiveDarkMode,
            ref useDarkMode,
            Marshal.SizeOf<int>()
        );

        ApplyColor(
            windowHandle,
            CaptionColor,
            isActive
                ? "TitleBarBackgroundBrush"
                : "TitleBarInactiveBackgroundBrush"
        );
        ApplyColor(
            windowHandle,
            TextColor,
            isActive
                ? "TitleBarTextBrush"
                : "TitleBarInactiveTextBrush"
        );
        ApplyColor(
            windowHandle,
            BorderColor,
            isActive
                ? "TitleBarBorderBrush"
                : "TitleBarInactiveBorderBrush"
        );
    }

    internal static uint ToColorReference(System.Windows.Media.Color color)
    {
        return color.R |
               ((uint)color.G << 8) |
               ((uint)color.B << 16);
    }

    private static void ApplyColor(
        IntPtr windowHandle,
        int attribute,
        string resourceKey)
    {
        if (System.Windows.Application.Current.TryFindResource(resourceKey) is not
            SolidColorBrush brush)
        {
            return;
        }

        uint color = ToColorReference(brush.Color);

        _ = DwmSetWindowAttributeColor(
            windowHandle,
            attribute,
            ref color,
            Marshal.SizeOf<uint>()
        );
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(
        IntPtr windowHandle,
        int attribute,
        ref int attributeValue,
        int attributeSize
    );

    [DllImport(
        "dwmapi.dll",
        EntryPoint = "DwmSetWindowAttribute",
        ExactSpelling = true
    )]
    private static extern int DwmSetWindowAttributeColor(
        IntPtr windowHandle,
        int attribute,
        ref uint attributeValue,
        int attributeSize
    );
}
