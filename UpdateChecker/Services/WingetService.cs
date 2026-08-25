using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

public sealed class WingetService
{
    private static readonly Regex AnsiEscapeRegex = new(
        @"\x1B\[[0-?]*[ -/]*[@-~]",
        RegexOptions.Compiled
    );

    public async Task<IReadOnlyList<AppUpdateInfo>> GetAvailableUpdatesAsync(CancellationToken cT = default)
    {
        string wingetPath = ResolveWingetPath();

        var startInfo = new ProcessStartInfo
        {
            FileName = wingetPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        startInfo.ArgumentList.Add("list");
        startInfo.ArgumentList.Add("--upgrade-available");
        startInfo.ArgumentList.Add("--accept-source-agreements");
        startInfo.ArgumentList.Add("--disable-interactivity");

        using var process = new Process
        {
            StartInfo = startInfo
        };

        if (!process.Start())
        {
            throw new InvalidOperationException(
                "WinGet could not be started."
            );
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process.WaitForExitAsync(cT);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw;
        }

        string output = await outputTask;
        File.WriteAllText(
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                "winget-output.txt"
            ),
            output
        );
        string error = await errorTask;

        IReadOnlyList<AppUpdateInfo> updates = ParseOutput(output);

        if (process.ExitCode != 0 &&
            updates.Count == 0 &&
            !string.IsNullOrWhiteSpace(error))
        {
            throw new InvalidOperationException(
                $"WinGet returned an error:{Environment.NewLine}{error.Trim()}"
            );
        }

        return updates;
    }

    private static string ResolveWingetPath()
    {
        string localAppData = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );

        string aliasPath = Path.Combine(
            localAppData,
            "Microsoft",
            "WindowsApps",
            "winget.exe"
        );

        if (File.Exists(aliasPath))
        {
            return aliasPath;
        }

        // Fall back to PATH lookup.
        return "winget.exe";
    }

     private static IReadOnlyList<AppUpdateInfo> ParseOutput(string output)
    {
        string cleanedOutput = AnsiEscapeRegex
            .Replace(output, string.Empty)
            .Replace("\r", string.Empty)
            .Replace("\b", string.Empty);

        string[] lines = cleanedOutput.Split('\n');

        int headerIndex = Array.FindIndex(
            lines,
            line =>
                line.Contains("Name", StringComparison.Ordinal) &&
                line.Contains("Id", StringComparison.Ordinal) &&
                line.Contains("Version", StringComparison.Ordinal) &&
                line.Contains("Available", StringComparison.Ordinal) &&
                line.Contains("Source", StringComparison.Ordinal)
        );

        if (headerIndex < 0)
        {
            return Array.Empty<AppUpdateInfo>();
        }

        string header = lines[headerIndex];

        int nameStart = header.IndexOf(
            "Name",
            StringComparison.Ordinal
        );

        int idStart = header.IndexOf(
            "Id",
            nameStart + 4,
            StringComparison.Ordinal
        );

        int versionStart = header.IndexOf(
            "Version",
            idStart + 2,
            StringComparison.Ordinal
        );

        int availableStart = header.IndexOf(
            "Available",
            versionStart + 7,
            StringComparison.Ordinal
        );

        int sourceStart = header.IndexOf(
            "Source",
            availableStart + 9,
            StringComparison.Ordinal
        );

        if (nameStart < 0 || idStart < 0 || versionStart < 0 || availableStart < 0 || sourceStart < 0)
            return Array.Empty<AppUpdateInfo>();
        
        int[] columnStarts =
        {
            nameStart,
            idStart,
            versionStart,
            availableStart,
            sourceStart
        };

        var updates = new List<AppUpdateInfo>();

        for (int index = headerIndex + 1; index < lines.Length; index++)
        {
            string line = lines[index].TrimEnd();

            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (IsSeparatorLine(line))
                continue;

            if (line.Contains("upgrades available", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("upgrade available", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("package(s) have version numbers", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            string[] fields = ExtractColumns(line, columnStarts);

            string name = fields[0];
            string id = fields[1];
            string installedVersion = fields[2];
            string availableVersion = fields[3];
            string source = fields[4];

            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(id) ||
                string.IsNullOrWhiteSpace(installedVersion) || string.IsNullOrWhiteSpace(availableVersion))
            {
                continue;
            }

            updates.Add(
                new AppUpdateInfo(
                    Name: name,
                    Id: id,
                    InstalledVersion: installedVersion,
                    AvailableVersion: availableVersion,
                    Source: source
                )
            );
        }

        return updates;
    }

    private static bool IsSeparatorLine(string line)
    {
        string trimmedLine = line.Trim();

        return trimmedLine.Length > 0 && trimmedLine.All(character => character == '-');
    }

    private static string[] ExtractColumns(string line, IReadOnlyList<int> columnStarts)
    {
        var fields = new string[columnStarts.Count];

        for (int index = 0; index < columnStarts.Count; index++)
        {
            int start = columnStarts[index];

            if (start >= line.Length)
            {
                fields[index] = string.Empty;
                continue;
            }

            int end = index + 1 < columnStarts.Count
                ? Math.Min(columnStarts[index + 1], line.Length)
                : line.Length;

            fields[index] = line[start..end].Trim();
        }

        return fields;
    }
}