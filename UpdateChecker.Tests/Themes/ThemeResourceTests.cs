using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace UpdateChecker.Tests.Themes;

public sealed partial class ThemeResourceTests
{
    [Fact]
    public void LightAndDarkThemes_ExposeTheSameResourceKeys()
    {
        HashSet<string> lightKeys = ReadThemeKeys("LightTheme.xaml");
        HashSet<string> darkKeys = ReadThemeKeys("DarkTheme.xaml");

        Assert.NotEmpty(lightKeys);
        Assert.Equal(lightKeys, darkKeys);
    }

    [Fact]
    public void UserInterface_OnlyReferencesResourcesProvidedByBothThemes()
    {
        HashSet<string> themeKeys = ReadThemeKeys("LightTheme.xaml");
        string userInterfaceXaml = string.Join(
            Environment.NewLine,
            File.ReadAllText(GetThemePath("Controls.xaml")),
            File.ReadAllText(GetViewPath("MainWindow.xaml")),
            File.ReadAllText(GetViewPath("SettingsView.xaml"))
        );
        string[] referencedKeys = DynamicResourceRegex()
            .Matches(userInterfaceXaml)
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(referencedKeys);
        Assert.All(referencedKeys, key => Assert.Contains(key, themeKeys));
    }

    [Fact]
    public void MainWindow_UsesAnApplicationRootIconUri()
    {
        XDocument mainWindow = XDocument.Load(
            GetViewPath("MainWindow.xaml")
        );
        string? iconUri = mainWindow.Root?.Attribute("Icon")?.Value;

        Assert.Equal(
            "/UpdateChecker;component/Assets/app_logo.ico",
            iconUri
        );
    }

    private static HashSet<string> ReadThemeKeys(string fileName)
    {
        XNamespace xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

        return XDocument
            .Load(GetThemePath(fileName))
            .Descendants()
            .Select(element => element.Attribute(xaml + "Key")?.Value)
            .Where(key => key is not null)
            .Select(key => key!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string GetThemePath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Themes", fileName);
    }

    private static string GetViewPath(string fileName)
    {
        return Path.Combine(AppContext.BaseDirectory, "Views", fileName);
    }

    [GeneratedRegex(@"\{DynamicResource\s+([^}]+)\}")]
    private static partial Regex DynamicResourceRegex();
}
