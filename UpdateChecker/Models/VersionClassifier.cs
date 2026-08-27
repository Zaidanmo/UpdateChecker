namespace UpdateChecker.Models;

internal static class VersionClassifier
{
    public static bool HasMajorVersionChange(
        string installedVersion,
        string availableVersion)
    {
        return GetLeadingVersionNumber(installedVersion) is long installedMajor &&
               GetLeadingVersionNumber(availableVersion) is long availableMajor &&
               installedMajor != availableMajor;
    }

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
