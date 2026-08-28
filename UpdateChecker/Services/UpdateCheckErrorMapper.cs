namespace UpdateChecker.Services;

internal sealed record UpdateCheckFailure(
    string StatusMessage,
    string Title,
    string Message,
    string NotificationMessage,
    bool IsUnexpected = false);

internal static class UpdateCheckErrorMapper
{
    public static UpdateCheckFailure FromException(Exception exception)
    {
        return exception switch
        {
            WingetUnavailableException => new UpdateCheckFailure(
                "WinGet is not available on this PC.",
                "WinGet is not available",
                "WinGet is required to check for application updates. " +
                "Install Microsoft App Installer from the Microsoft Store, " +
                "then restart this app.",
                "Install Microsoft App Installer to enable update checks."
            ),
            WingetAccessDeniedException => new UpdateCheckFailure(
                "Windows blocked access to WinGet.",
                "WinGet access was denied",
                "Windows prevented this app from starting WinGet. " +
                "Check your security policy or contact your administrator, " +
                "then try again.",
                "Windows blocked access to WinGet. Review your security policy."
            ),
            WingetCommandException commandException => MapCommandFailure(
                commandException
            ),
            WingetOutputParseException => new UpdateCheckFailure(
                "The WinGet response could not be read.",
                "WinGet response could not be read",
                "WinGet returned information in a format this version of " +
                "the app does not recognize. Update WinGet and this app, " +
                "then try again.",
                "Update WinGet and App Update Checker, then try again."
            ),
            WingetOutputLimitExceededException => new UpdateCheckFailure(
                "WinGet returned too much information.",
                "WinGet response was too large",
                "WinGet returned more information than the app can safely " +
                "process. Restart WinGet and try again.",
                "WinGet returned too much information. Try again later."
            ),
            _ => new UpdateCheckFailure(
                "An unexpected error interrupted the update check.",
                "Unexpected update-check error",
                "An unexpected error occurred while checking for updates. " +
                "Please try again." +
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Details: {GetSafeDetails(exception.Message)}",
                "An unexpected error interrupted the update check.",
                IsUnexpected: true
            )
        };
    }

    internal static string GetSafeDetails(string details)
    {
        const int maximumLength = 500;

        string normalizedDetails = details
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();

        return normalizedDetails.Length <= maximumLength
            ? normalizedDetails
            : $"{normalizedDetails[..maximumLength]}...";
    }

    private static UpdateCheckFailure MapCommandFailure(
        WingetCommandException exception)
    {
        string details = GetSafeDetails(exception.Details);
        string message =
            "WinGet could not complete the scan. Check your internet " +
            "connection and WinGet sources, then try again." +
            $"{Environment.NewLine}{Environment.NewLine}" +
            $"Exit code: {exception.ExitCode}";

        if (!string.IsNullOrWhiteSpace(details))
        {
            message +=
                $"{Environment.NewLine}{Environment.NewLine}" +
                $"Details: {details}";
        }

        return new UpdateCheckFailure(
            "WinGet could not complete the update check.",
            "Update check could not be completed",
            message,
            "Check your connection and WinGet sources, then try again."
        );
    }
}
