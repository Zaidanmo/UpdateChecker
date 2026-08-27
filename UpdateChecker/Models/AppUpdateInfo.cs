namespace UpdateChecker.Models;

public sealed record AppUpdateInfo(
    string Name,
    string Id,
    string InstalledVersion,
    string AvailableVersion)
{
    public bool HasMajorVersionChange =>
        VersionClassifier.HasMajorVersionChange(
            InstalledVersion,
            AvailableVersion
        );
}
