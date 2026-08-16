# Publishing Zenmanage to NuGet

This guide covers the remaining work needed to publish the .NET SDK from this repository to public NuGet.

## 1. Install the .NET SDK Locally or in CI

The current machine did not have the `dotnet` CLI available during implementation, so the package could not be restored, built, tested, or packed here.

Install .NET 8 SDK and verify:

```bash
dotnet --info
```

## 2. Confirm Package Identity

Review these values before the first public release:

- `PackageId` in `src/Zenmanage/Zenmanage.csproj`
- `Version`, `AssemblyVersion`, and `FileVersion`
- `RepositoryUrl` and `PackageProjectUrl` in `Directory.Build.props`
- `Description`, tags, and author metadata

Also confirm that the `Zenmanage` package ID is still available on NuGet. If not, rename the package before publishing.

## 3. Restore, Build, and Test

Run the standard validation flow:

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release --collect:"XPlat Code Coverage"
```

Recommended additions:

- Enforce a coverage floor in CI.
- Publish TRX and Cobertura artifacts.
- Fail the pipeline on warnings.

## 4. Pack the NuGet Artifacts

Create the `.nupkg` and symbol package:

```bash
dotnet pack src/Zenmanage/Zenmanage.csproj -c Release -o ./artifacts
```

You should see:

- `Zenmanage.<version>.nupkg`
- `Zenmanage.<version>.snupkg`

## 5. Validate the Package Before Uploading

Check the package locally:

```bash
dotnet nuget verify ./artifacts/Zenmanage.<version>.nupkg
```

Review the package contents to confirm:

- README is included.
- XML docs are included.
- Portable PDBs and SourceLink data are present.
- Package metadata is accurate.

## 6. Create a NuGet API Key

In nuget.org:

1. Create or sign in to the publishing account.
2. Generate a scoped API key.
3. Limit the key to the package ID if possible.
4. Store the key in CI as a secret.

## 7. Publish to NuGet

Publish the package:

```bash
dotnet nuget push ./artifacts/Zenmanage.<version>.nupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

Publish symbols if needed:

```bash
dotnet nuget push ./artifacts/Zenmanage.<version>.snupkg \
  --api-key "$NUGET_API_KEY" \
  --source https://api.nuget.org/v3/index.json
```

## 8. Add CI Release Automation

Recommended GitHub Actions flow:

1. Trigger on version tags like `v0.1.0`.
2. Install .NET 8 SDK.
3. Restore, build, test, and collect coverage.
4. Pack in `Release`.
5. Push to NuGet using a repository secret.
6. Attach artifacts to the GitHub release.

## 8.1 Branch Protection Recommendations

Before publishing from tags, protect your default branch (for example, `main`) so only validated code can be merged.

Recommended GitHub branch protection settings:

- Require a pull request before merging.
- Require status checks to pass before merging.
- Mark these checks as required:
  - `Build and Test (.NET 8)` from the CI workflow.
- Require branches to be up to date before merging.
- Require conversation resolution before merging.
- Restrict who can push directly to the protected branch.
- Optionally require signed commits if your team policy mandates it.

Suggested release controls:

- Limit who can create version tags (`v*`).
- Use GitHub environments with required reviewers for publish jobs.
- Keep `NUGET_API_KEY` as an environment or repository secret only.

## 9. Tighten the Public Package Before GA

Recommended follow-up work before a stable `1.0.0` release:

- Add integration tests against a mocked HTTP server or captured fixtures.
- Add GitHub Actions for CI and tagged-release publishing.
- Verify package compatibility targets if you want support beyond `.NET 8`.
- Decide whether the package should stay server-first or split client/server packages.
- Add repository badges to the root README once CI exists.

## 10. First Release Checklist

- `dotnet restore` passes
- `dotnet build -c Release` passes
- `dotnet test -c Release` passes
- Coverage reviewed
- NuGet metadata verified
- Package ID availability confirmed
- README rendered correctly on NuGet preview
- Tag created and published