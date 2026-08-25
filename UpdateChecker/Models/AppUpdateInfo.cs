namespace UpdateChecker.Models;

public sealed record AppUpdateInfo(
    string Name,
    string Id,
    string InstalledVersion,
    string AvailableVersion,
    string Source)
{
    public bool IsMajorUpdate =>
        GetLeadingVersionNumber(InstalledVersion) is long installedMajor &&
        GetLeadingVersionNumber(AvailableVersion) is long availableMajor &&
        installedMajor != availableMajor;

    public bool IsUpdateAvailable =>
        !string.IsNullOrWhiteSpace(AvailableVersion) &&
        !string.Equals(
            InstalledVersion,
            AvailableVersion,
            StringComparison.OrdinalIgnoreCase
        );

    private static long? GetLeadingVersionNumber(string version)
    {
        ReadOnlySpan<char> value = version.AsSpan();
        int numberStart = -1;

        for (int index = 0; index < value.Length; index++)
        {
            if (char.IsDigit(value[index]))
            {
                numberStart = index;
                break;
            }
        }

        if (numberStart < 0)
        {
            return null;
        }

        int numberEnd = numberStart;

        while (numberEnd < value.Length && char.IsDigit(value[numberEnd]))
        {
            numberEnd++;
        }

        return long.TryParse(
            value[numberStart..numberEnd],
            out long leadingNumber
        )
            ? leadingNumber
            : null;
    }
}
