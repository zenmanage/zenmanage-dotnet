# Zenmanage .NET SDK Examples

This folder contains small, self-contained sample programs that mirror the examples shipped in the PHP and JavaScript SDKs.

## Example Set

- `simple-flags.cs`
- `context-based-flags.cs`
- `defaults.cs`
- `caching.cs`
- `ab-testing.cs`
- `percentage-rollouts.cs`
- `aspnet-core.cs` — ASP.NET Core dependency injection and per-request middleware

## Running the Examples

Create a console app, add a reference to the SDK, then paste in the sample you want to try.

```bash
dotnet new console -n ZenmanageSamples
cd ZenmanageSamples
dotnet add package Zenmanage
```

If you are testing the package from this repository before publishing, use a project reference instead:

```bash
dotnet add reference ../src/Zenmanage/Zenmanage.csproj
```

Most examples expect an environment token to be available. Replace the placeholder token directly in the sample or read it from configuration.