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

### Json Flags

```csharp
var config = await zenmanage.Flags().SingleAsync("checkout-config", new Dictionary<string, object>
{
    ["maxRetries"] = 3,
    ["providers"] = new[] { "stripe", "paypal" }
});

var maxRetries = config.AsJson().GetProperty("maxRetries").GetInt32();
```

A json default can be an inline `IDictionary`/`IEnumerable` (list, array, etc.), a
`System.Text.Json.JsonElement`, or registered on a `DefaultsCollection` via
`Add(key, JsonElement)`.

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

## Value Types & Cross-Type Coercion

A flag's type is one of `Boolean`, `String`, `Number`, or `Json`. Each has a matching accessor (`AsBool()`, `AsString()`, `AsNumber()`, `AsJson()`), plus `IsEnabled()` for boolean flags specifically.

**`AsJson()`** returns the decoded value as a `System.Text.Json.JsonElement`, covering both JSON objects and JSON arrays.

**Calling the "wrong" accessor for a flag's type never throws** — it returns a safe zero value for that type instead of attempting a lossy conversion:

| Called on → Flag type ↓ | `AsBool()` | `AsString()` | `AsNumber()` | `AsJson()` |
|---|---|---|---|---|
| `Boolean` | the bool | `"true"`/`"false"` | `1`/`0` | empty array |
| `String` | `true` if non-empty | the string | parsed number, or `0` | empty array |
| `Number` | `true` if non-zero | the number as a string | the number | empty array |
| `Json` | `false` | `""` | `0` | the decoded value |

A `Json` flag whose decoded value isn't itself an object or array (a bare JSON scalar such as `5` or `"x"`, which the API allows but which real flags won't normally use) makes `AsJson()` return an empty array too — the same safe-zero-value fallback as calling it on a non-json flag, rather than wrapping the scalar or throwing.

**Default values** passed to `SingleAsync(key, defaultValue)` or `DefaultsCollection` are typed from the .NET value itself: an `IDictionary`, an `IEnumerable` (excluding `string`), or a `JsonElement` holding an object/array becomes a `Json`-typed flag (not a stringified fallback), so `AsJson()` on a missing flag with such a default returns that value unchanged.

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