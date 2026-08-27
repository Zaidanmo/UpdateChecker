using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal sealed class WingetService : IUpdateSource
{
    public async Task<IReadOnlyList<AppUpdateInfo>> GetAvailableUpdatesAsync(
        CancellationToken cancellationToken = default)
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

        StartWingetProcess(process);

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
        Task<string> errorTask = process.StandardError.ReadToEndAsync();

        try
        {
            await process
                .WaitForExitAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await StopProcessAsync(process).ConfigureAwait(false);
            await DrainOutputAsync(outputTask, errorTask).ConfigureAwait(false);
            throw;
        }

        string output = await outputTask.ConfigureAwait(false);
        string error = await errorTask.ConfigureAwait(false);

        EnsureSuccessfulExit(process.ExitCode, error);

        return WingetOutputParser.Parse(output);
    }

    private static async Task StopProcessAsync(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            // The process exited between HasExited and Kill.
        }
        catch (Win32Exception) when (process.HasExited)
        {
            // Windows reports a missing process when it has already exited.
        }

        await process
            .WaitForExitAsync(CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static async Task DrainOutputAsync(params Task<string>[] outputTasks)
    {
        try
        {
            await Task.WhenAll(outputTasks).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Closing a cancelled process can close its redirected streams.
        }
        catch (ObjectDisposedException)
        {
            // The process streams were disposed during cancellation.
        }
    }

    private static void EnsureSuccessfulExit(int exitCode, string error)
    {
        if (exitCode == 0)
            return;

        throw new WingetCommandException(exitCode, error.Trim());
    }

    private static void StartWingetProcess(Process process)
    {
        try
        {
            if (!process.Start())
            {
                throw new WingetUnavailableException();
            }
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode is 2 or 3)
        {
            throw new WingetUnavailableException(exception);
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 5)
        {
            throw new WingetAccessDeniedException(exception);
        }
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

}
