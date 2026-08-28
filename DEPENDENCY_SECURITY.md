# Dependency security

The production application has no third-party `PackageReference` dependencies.
The test project uses a small, explicitly reviewed set of packages from
`nuget.org`.

The current xUnit v2 package is marked as legacy because feature development has
moved to xUnit v3. It is test-only, has no known vulnerability, and its publisher
continues to provide security fixes. Migrating to v3 also changes the test-host
model, so that migration should be reviewed and tested as a separate change
rather than being hidden inside dependency hardening.

## Protections

- `NuGet.config` clears inherited feeds and maps every approved package ID to
  `nuget.org`. A new direct or transitive package cannot restore until its ID is
  reviewed and added to the mapping.
- Packages restore into the repository-local, ignored `.nuget/packages` cache.
  This prevents a package already present in a user-wide cache from bypassing
  source mapping.
- Each project's `packages.lock.json` records the complete dependency graph and
  package content hashes. Restore runs in locked mode whenever a lock file is
  present.
- NuGet audits direct and transitive dependencies. Advisories at low, moderate,
  high, or critical severity fail restore and the build (`NU1901`-`NU1904`).
- Test-only dependencies use `PrivateAssets="all"` and consume only the asset
  categories required to compile and run the tests. Package analyzers,
  `contentFiles`, and unnecessary transitive build assets are not loaded.
- The test SDK's transitive code-coverage package remains hash-locked but all of
  its executable assets are excluded because this project does not collect code
  coverage.

## Adding or updating a package

1. Confirm the exact package ID, owner, repository, release history, and
   reserved-prefix status on `nuget.org`.
2. Download and inspect the `.nupkg`. Pay particular attention to `build`,
   `buildMultitargeting`, `buildTransitive`, `analyzers`, and `tools`.
3. Add the exact package ID and trusted source to `NuGet.config`. Do not add a
   wildcard mapping merely to make restore succeed.
4. Add or update the exact `PackageReference` version and restrict
   `IncludeAssets`/`PrivateAssets` to what the project actually requires.
5. Explicitly regenerate the graph:

   ```powershell
   dotnet restore UpdateChecker.sln --force-evaluate
   ```

6. Review every change to `packages.lock.json`, including new transitive
   dependencies and `contentHash` values. Run the full Release test suite before
   committing.

Do not suppress a NuGet vulnerability warning without documenting why the
specific advisory cannot affect this application and when the exception will be
removed.
