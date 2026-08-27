using System.Windows.Media;
using UpdateChecker.Services;
using Xunit;

namespace UpdateChecker.Tests.Services;

public sealed class WindowTitleBarServiceTests
{
    [Fact]
    public void ToColorReference_UsesWindowsColorByteOrder()
    {
        Color color = Color.FromRgb(0x11, 0x22, 0x33);

        uint colorReference = WindowTitleBarService.ToColorReference(color);

        Assert.Equal(0x00332211u, colorReference);
    }
}
