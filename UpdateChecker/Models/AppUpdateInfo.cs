namespace UpdateChecker.Models;

public sealed record AppUpdateInfo(
    string Name,
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string Source)
{
    public bool IsUpdateAvailable =>
        !string.IsNullOrWhiteSpace(AvailableVersion) &&
        !string.Equals(
            InstalledVersion,
            AvailableVersion,
            StringComparison.OrdinalIgnoreCase
        );
}