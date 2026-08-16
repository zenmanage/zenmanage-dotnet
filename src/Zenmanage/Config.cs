using Zenmanage.Caching;
using Microsoft.Extensions.Logging;

namespace Zenmanage;

/// <summary>
/// Immutable runtime configuration for the Zenmanage SDK.
/// </summary>
public sealed record Config(
    string EnvironmentToken,
    int CacheTtl = 3600,
    CacheBackend CacheBackend = CacheBackend.Memory,
    string? CacheDirectory = null,
    bool EnableUsageReporting = true,
    string ApiEndpoint = "https://api.zenmanage.com",
    ILogger? Logger = null,
    IZenmanageCache? CustomCache = null,
    Func<HttpClient>? HttpClientFactory = null,
    RuntimeEnvironment RuntimeEnvironment = RuntimeEnvironment.Server,
    string? ClientAgent = null);