using System.Buffers;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using UpdateChecker.Models;

namespace UpdateChecker.Services;

internal sealed class WingetService : IUpdateSource
{
    private const int MaximumStandardOutputCharacters = 4 * 1024 * 1024;
    private const int MaximumStandardErrorCharacters = 256 * 1024;
    private const int ReadBufferSize = 4096;

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

        Task<BoundedText> outputTask = ReadBoundedAsync(
            process.StandardOutput,
            MaximumStandardOutputCharacters,
            cancellationToken
        );
        Task<BoundedText> errorTask = ReadBoundedAsync(
            process.StandardError,
            MaximumStandardErrorCharacters,
            cancellationToken
        );

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

        BoundedText output = await outputTask.ConfigureAwait(false);
        BoundedText error = await errorTask.ConfigureAwait(false);

        if (output.WasTruncated)
        {
            throw new WingetOutputLimitExceededException();
        }

        EnsureSuccessfulExit(process.ExitCode, error.Value);

        return WingetOutputParser.Parse(output.Value);
    }

    internal static async Task<BoundedText> ReadBoundedAsync(
        TextReader reader,
        int maximumCharacters,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCharacters);

        char[] buffer = ArrayPool<char>.Shared.Rent(ReadBufferSize);
        var value = new StringBuilder(
            capacity: Math.Min(maximumCharacters, ReadBufferSize)
        );
        bool wasTruncated = false;

        try
        {
            int charactersRead;

            while ((charactersRead = await reader.ReadAsync(
                       buffer.AsMemory(0, ReadBufferSize),
                       cancellationToken
                   ).ConfigureAwait(false)) > 0)
            {
                int charactersToKeep = Math.Min(
                    charactersRead,
                    maximumCharacters - value.Length
                );

                if (charactersToKeep > 0)
                {
                    value.Append(buffer, 0, charactersToKeep);
                }

                wasTruncated |= charactersToKeep < charactersRead;
            }
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }

        return new BoundedText(value.ToString(), wasTruncated);
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

    private static async Task DrainOutputAsync(params Task[] outputTasks)
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
        catch (OperationCanceledException)
        {
            // Both stream readers use the process cancellation token.
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

internal readonly record struct BoundedText(
    string Value,
    bool WasTruncated);
