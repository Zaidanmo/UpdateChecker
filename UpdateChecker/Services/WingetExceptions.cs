namespace UpdateChecker.Services;

internal sealed class WingetUnavailableException : Exception
{
    public WingetUnavailableException(Exception? innerException = null)
        : base("WinGet is not installed or could not be found.", innerException)
    { }
}

internal sealed class WingetAccessDeniedException : Exception
{
    public WingetAccessDeniedException(Exception innerException)
        : base("Windows prevented the app from starting WinGet.", innerException)
    { }
}

internal sealed class WingetCommandException : Exception
{
    public WingetCommandException(int exitCode, string details)
        : base($"WinGet exited with code {exitCode}.")
    {
        ExitCode = exitCode;
        Details = details;
    }

    public int ExitCode { get; }

    public string Details { get; }
}

internal sealed class WingetOutputParseException : Exception
{
    public WingetOutputParseException()
        : base("WinGet returned output in an unsupported format.")
    { }
}
