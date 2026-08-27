using System.Windows;

using UpdateChecker.Services;

namespace UpdateChecker;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ThemeManager.Initialize();
        base.OnStartup(e);
    }
}
