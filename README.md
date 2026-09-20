# Zenmanage .NET SDK

[![CI](https://github.com/zenmanage/zenmanage-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/zenmanage/zenmanage-dotnet/actions/workflows/ci.yml)
[![Release](https://github.com/zenmanage/zenmanage-dotnet/actions/workflows/release.yml/badge.svg)](https://github.com/zenmanage/zenmanage-dotnet/actions/workflows/release.yml)
[![NuGet](https://img.shields.io/nuget/v/Zenmanage.svg)](https://www.nuget.org/packages/Zenmanage)

Add feature flags to your .NET applications in minutes. Control feature rollouts, A/B tests, and configuration values without deploying code.

## Why Zenmanage?

- Fast local rule evaluation after rules are cached.
- Target users, organizations, and custom segments with context-aware rules.
- Roll out features safely with deterministic percentage bucketing.
- Use inline defaults or reusable defaults collections to avoid brittle code paths.
- Package layout and metadata are ready for public NuGet publishing.

## Requirements

- .NET 8.0+

## Installation

```bash
dotnet add package Zenmanage
```

## Key Compatibility

- Supported in server runtime mode: server keys prefixed with `srv_`
- Supported in client runtime mode: client keys prefixed with `cli_`
- Not supported: mobile keys prefixed with `mob_`

By default, `ConfigBuilder` validates for a server runtime. If you need client-key validation, call `WithRuntimeEnvironment(RuntimeEnvironment.Client)` explicitly.

## Get Started

```csharp
using Zenmanage;

var zenmanage = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .Build());

var flag = await zenmanage.Flags().SingleAsync("new-dashboard");

if (flag.IsEnabled())
{
    Console.WriteLine("Render the new dashboard");
}
else
{
    Console.WriteLine("Render the classic dashboard");
}
```

## Common Use Cases

### Gradual Rollouts

```csharp
using Zenmanage.Contexting;

var context = Context.Single("user", "user-123", "Jane Doe");

var rolloutFlag = await zenmanage.Flags()
    .WithContext(context)
    .SingleAsync("beta-program");

if (rolloutFlag.IsEnabled())
{
    Console.WriteLine("User is part of the beta rollout");
}
```

### A/B Testing

```csharp
using Zenmanage.Contexting;

var context = new Context(
    "user",
    name: "Jane Doe",
    identifier: "user-123",
    attributes: new[]
    {
        new Attribute("country", new[] { "US" }),
        new Attribute("plan", new[] { "premium" })
    });

var variant = await zenmanage.Flags()
    .WithContext(context)
    .SingleAsync("checkout-flow", "control");

Console.WriteLine($"Variant: {variant.AsString()}");
```

### Defaults

```csharp
using Zenmanage.Defaults;

var defaults = new DefaultsCollection()
    .Add("new-ui", true)
    .Add("api-version", "v2")
    .Add("max-items", 100);

var flag = await zenmanage.Flags()
    .WithDefaults(defaults)
    .SingleAsync("new-ui");

Console.WriteLine(flag.IsEnabled());
```

### Filesystem Caching

```csharp
using Zenmanage.Caching;

var sdk = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .WithCacheBackend(CacheBackend.Filesystem)
        .WithCacheDirectory(Path.Combine(AppContext.BaseDirectory, ".zenmanage-cache"))
        .Build());
```

### ASP.NET Core Registration

```csharp
using Zenmanage;
using Zenmanage.Caching;

builder.Services.AddSingleton(sp => new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken(builder.Configuration["Zenmanage:EnvironmentToken"]!)
        .WithCacheBackend(CacheBackend.Memory)
        .Build()));
```

## Configuration

```csharp
using Zenmanage;
using Zenmanage.Caching;

var config = ConfigBuilder.Create()
    .WithEnvironmentToken("srv_your_server_key_here")
    .WithCacheTtl(3600)
    .WithCacheBackend(CacheBackend.Memory)
    .WithUsageReporting(true)
    .WithApiEndpoint("https://api.zenmanage.com")
    .Build();
```

Supported environment variables:

- `ZENMANAGE_ENVIRONMENT_TOKEN`
- `ZENMANAGE_CACHE_TTL`
- `ZENMANAGE_CACHE_BACKEND`
- `ZENMANAGE_CACHE_DIR`
- `ZENMANAGE_ENABLE_USAGE_REPORTING`
- `ZENMANAGE_API_ENDPOINT`

## Running Tests

```bash
dotnet restore
dotnet test --collect:"XPlat Code Coverage"
```

## Examples

See [examples/README.md](examples/README.md) for the sample set:

- `simple-flags.cs`
- `context-based-flags.cs`
- `defaults.cs`
- `caching.cs`
- `ab-testing.cs`
- `percentage-rollouts.cs`
- `aspnet-core.cs`

## Publishing

See [docs/NUGET_PUBLISHING_GUIDE.md](docs/NUGET_PUBLISHING_GUIDE.md) for the release checklist and NuGet publication steps.