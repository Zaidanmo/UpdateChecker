using System.Text.RegularExpressions;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal static class WingetOutputParser
{
    private static readonly Regex AnsiEscapeRegex = new(
        @"\x1B\[[0-?]*[ -/]*[@-~]",
        RegexOptions.Compiled
    );

    private static readonly string[] NoUpgradeMessages =
    {
        "No installed package found matching input criteria.",
        "No applicable upgrade found.",
        "No package found matching input criteria."
    };

    public static IReadOnlyList<AppUpdateInfo> Parse(string output)
    {
        string cleanedOutput = CleanOutput(output);

        if (ContainsNoUpgradeMessage(cleanedOutput))
        {
            return Array.Empty<AppUpdateInfo>();
        }

        string[] lines = cleanedOutput.Split('\n');
        int headerIndex = FindHeader(lines, out ColumnLayout columns);

        if (headerIndex < 0)
        {
            throw new WingetOutputParseException();
        }

        var updates = new List<AppUpdateInfo>();

        for (int index = headerIndex + 1; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd();

            if (string.IsNullOrWhiteSpace(line) || IsSeparatorLine(line))
            {
                continue;
            }

            if (IsSummaryLine(line))
            {
                break;
            }

            AppUpdateInfo? update = ParseUpdate(line, columns);

            if (update is not null)
            {
                updates.Add(update);
            }
        }

        return updates;
    }

    private static string CleanOutput(string output)
    {
        return AnsiEscapeRegex
            .Replace(output, string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\b", string.Empty);
    }

    private static bool ContainsNoUpgradeMessage(string output)
    {
        return NoUpgradeMessages.Any(
            message => output.Contains(
                message,
                StringComparison.OrdinalIgnoreCase
            )
        );
    }

    private static int FindHeader(
        IReadOnlyList<string> lines,
        out ColumnLayout columns)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            if (TryGetColumnLayout(lines[index], out columns))
            {
                return index;
            }
        }

        columns = default;
        return -1;
    }

    private static bool TryGetColumnLayout(
        string header,
        out ColumnLayout columns)
    {
        int nameStart = header.IndexOf("Name", StringComparison.Ordinal);
        int idStart = nameStart < 0
            ? -1
            : header.IndexOf("Id", nameStart + "Name".Length, StringComparison.Ordinal);
        int versionStart = idStart < 0
            ? -1
            : header.IndexOf("Version", idStart + "Id".Length, StringComparison.Ordinal);
        int availableStart = versionStart < 0
            ? -1
            : header.IndexOf(
                "Available",
                versionStart + "Version".Length,
                StringComparison.Ordinal
            );
        int sourceStart = availableStart < 0
            ? -1
            : header.IndexOf(
                "Source",
                availableStart + "Available".Length,
                StringComparison.Ordinal
            );

        if (nameStart < 0 || idStart < 0 || versionStart < 0 || availableStart < 0)
        {
            columns = default;
            return false;
        }

        columns = new ColumnLayout(
            nameStart,
            idStart,
            versionStart,
            availableStart,
            sourceStart
        );
        return true;
    }

    private static AppUpdateInfo? ParseUpdate(
        string line,
        ColumnLayout columns)
    {
        string name = ExtractColumn(line, columns.Name, columns.Id);
        string id = ExtractColumn(line, columns.Id, columns.Version);
        string installedVersion = ExtractColumn(
            line,
            columns.Version,
            columns.Available
        );
        string availableVersion = ExtractColumn(
            line,
            columns.Available,
            columns.Source >= 0 ? columns.Source : line.Length
        );

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(id) ||
            string.IsNullOrWhiteSpace(installedVersion) ||
            string.IsNullOrWhiteSpace(availableVersion))
        {
            return null;
        }

        return new AppUpdateInfo(
            Name: name,
            Id: id,
            InstalledVersion: installedVersion,
            AvailableVersion: availableVersion
        );
    }

    private static string ExtractColumn(string line, int start, int end)
    {
        if (start >= line.Length)
        {
            return string.Empty;
        }

        return line[start..Math.Min(end, line.Length)].Trim();
    }

    private static bool IsSeparatorLine(string line)
    {
        string trimmedLine = line.Trim();

        return trimmedLine.Length > 0 &&
               trimmedLine.All(character => character == '-');
    }

    private static bool IsSummaryLine(string line)
    {
        return line.Contains(
                   "upgrades available",
                   StringComparison.OrdinalIgnoreCase
               ) ||
               line.Contains(
                   "upgrade available",
                   StringComparison.OrdinalIgnoreCase
               ) ||
               line.Contains(
                   "package(s) have version numbers",
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private readonly record struct ColumnLayout(
        int Name,
        int Id,
        int Version,
        int Available,
        int Source
    );
}
