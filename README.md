# App Update Checker

App Update Checker is a lightweight Windows desktop application that scans the
applications recognized by Windows Package Manager (`winget`) and presents
available updates in a clean, readable interface.

The project was created to understand what a basic update-management tool could
look like in .NET, how NuGet-managed resources and dependencies fit into the
development process, and how WinGet can be used to discover application
updates. It is intentionally focused, but there are many features that could be
added or expanded in the future.

> **Important:** App Update Checker reports available updates and can copy an
> exact WinGet update command. It does not silently install updates or replace
> Windows Package Manager.

## Features

- Checks installed applications for updates through WinGet.
- Displays the application name, installed version, available version, and
  version-change status.
- Distinguishes minor updates from major version changes.
- Copies an exact WinGet update command for an individual application.
- Supports light and dark themes.
- Can optionally continue running in the Windows notification area.
- Supports optional scheduled checks every hour, 6 hours, 12 hours, day, or
  week.
- Shows a notification when scheduled checks find updates.
- Remembers the latest successful check and the selected settings.
- Includes clear warnings for missing WinGet, access problems, timeouts, and
  unsupported output.

Automatic checks and background operation are disabled by default. They must be
enabled by the user from Settings.

## Requirements

### Running the application

- A supported Windows 10 or Windows 11 installation.
- [Windows Package Manager (WinGet)](https://learn.microsoft.com/windows/package-manager/winget/).
  WinGet is normally installed through **Microsoft App Installer** from the
  Microsoft Store.
- The **.NET 10 Desktop Runtime** when using a framework-dependent build. A
  self-contained published build includes its own runtime and does not require
  this separate installation.

Confirm that WinGet is available by opening Windows Terminal, PowerShell, or
Command Prompt and running:

```powershell
winget --version
```

If Windows cannot find that command, install or update **App Installer** and
then restart App Update Checker.

### Building from source

- The [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- Git.
- Windows, because the project uses WPF and Windows notification-area APIs.

The application itself has no third-party runtime package dependencies. NuGet
is used for the test tooling, and the repository includes locked dependencies,
source mapping, and vulnerability auditing. See
[DEPENDENCY_SECURITY.md](DEPENDENCY_SECURITY.md) for the package-security policy.

## Build and test

```powershell
git clone <repository-url>
cd UpdateChecker
dotnet restore UpdateChecker.sln --locked-mode
dotnet build UpdateChecker.sln --configuration Release --no-restore
dotnet test UpdateChecker.sln --configuration Release --no-restore
```

To run the project directly:

```powershell
dotnet run --project UpdateChecker/UpdateChecker.csproj
```

## Basic usage

1. Start App Update Checker.
2. Select **Check for updates** or press `Ctrl+R`/`F5`.
3. Review the installed and available versions in the results table.
4. Use the copy button in the **Update command** column when you want to run an
   update manually.
5. Open Settings to configure background operation, automatic check intervals,
   and the application theme.

When background operation is enabled, closing the window hides the application
in the notification area. Use its notification-area menu to reopen it, check
again, or exit completely.

## Troubleshooting

### WinGet is not available

Install or update **Microsoft App Installer**, confirm that `winget --version`
works in a terminal, and restart the application.

### The check times out or fails

Run the following command in a terminal:

```powershell
winget list --upgrade-available
```

If that command also fails, repair WinGet, check its configured sources and
internet access, and try again. Managed PCs may require assistance from an
administrator.

### An application does not appear

The application can only display software that WinGet recognizes and reports as
upgradeable. Some applications use their own updater or are not registered with
a supported WinGet source.

## Reporting a problem or requesting a feature

Please open a GitHub issue with a clear, descriptive title. A useful report
should contain:

- Your Windows version.
- The App Update Checker version or commit you used.
- The result of `winget --version`.
- Whether the problem occurred during a manual or scheduled check.
- Exact steps to reproduce the problem.
- What you expected to happen and what actually happened.
- Any error shown by the application.
- A screenshot when it helps explain the issue.

You can use this format:

```text
Summary:

Windows version:
App/commit version:
WinGet version:

Steps to reproduce:
1.
2.
3.

Expected result:
Actual result:

Additional details:
```

Remove usernames, file paths, package-source credentials, tokens, and other
personal information before posting logs or screenshots. For a sensitive
security issue, use GitHub private vulnerability reporting when it is available
instead of publishing the details in a public issue.

Feature requests are welcome. Describe the problem the feature would solve and
how you imagine it fitting into the existing workflow.

## Project status and contributions

This is a learning project and a foundation for exploring update-management
ideas, not a replacement for an enterprise patch-management platform. Possible
future work includes richer filtering, release-note links, update history,
additional package sources, and more installation workflows.

Contributions, suggestions, bug reports, and improvements are welcome. If this
project or its ideas help your own work, attribution to the original project and
author is appreciated.

## AI-assisted development disclosure

AI tools were used as supporting tools during development. Their use was most
significant in designing, iterating, and polishing the user interface, including
layout, styling, theme behavior, and UX details. AI assistance was also used to
a smaller extent for code review, refactoring ideas, tests, and documentation.

The application remains a human-directed project: feature choices, final design
decisions, testing, and responsibility for the resulting code belong to the
project author.

## Disclaimer

Review an update command before running it. Application updates can introduce
breaking changes, and a major version difference is not automatically a security
severity rating. Use this project at your own discretion and keep appropriate
backups of important data.
