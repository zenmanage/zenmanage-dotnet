using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Api;
using Zenmanage.Caching;
using Zenmanage.Exceptions;
using Zenmanage.Flagging;
using Zenmanage.Internal;
using Zenmanage.Rules;

namespace Zenmanage;

/// <summary>
/// Main entry point for the Zenmanage SDK.
/// </summary>
public sealed class Zenmanage : IZenmanageClient
{
    private readonly ApiClient apiClient;
    private readonly IFlagManager flagManager;

    public Zenmanage(Config config)
    {
        var logger = config.Logger ?? NullLogger.Instance;
        var cache = CreateCache(config);
        var httpClient = config.HttpClientFactory?.Invoke();
        apiClient = new ApiClient(config.EnvironmentToken, config.ApiEndpoint, logger, config.EnableUsageReporting, httpClient, config.ClientAgent);
        var ruleEngine = new RuleEngine();

        flagManager = new FlagManager(apiClient, cache, ruleEngine, config.CacheTtl, logger);
    }

    /// <summary>
    /// Returns the flag manager used for retrieving and evaluating flags.
    /// </summary>
    public IFlagManager Flags() => flagManager;

    public void Dispose() => apiClient.Dispose();

    private static IZenmanageCache CreateCache(Config config)
    {
        if (config.CustomCache is not null)
        {
            return config.CustomCache;
        }

        return config.CacheBackend switch
        {
            CacheBackend.Memory => new InMemoryCache(),
            CacheBackend.Null => new NullCache(),
            CacheBackend.Filesystem => new FileSystemCache(config.CacheDirectory ?? throw new ConfigurationException("Cache directory is required for filesystem cache")),
            _ => throw new ConfigurationException($"Invalid cache backend: {config.CacheBackend}")
        };
    }
}