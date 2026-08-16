using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Caching;
using Zenmanage.Exceptions;

namespace Zenmanage;

/// <summary>
/// Fluent builder for constructing validated <see cref="Config"/> instances.
/// </summary>
public sealed class ConfigBuilder
{
    private const string ServerKeyPrefix = "srv_";
    private const string ClientKeyPrefix = "cli_";
    private const string MobileKeyPrefix = "mob_";

    private string? environmentToken;
    private int cacheTtl = 3600;
    private CacheBackend cacheBackend = CacheBackend.Memory;
    private string? cacheDirectory;
    private bool enableUsageReporting = true;
    private string apiEndpoint = "https://api.zenmanage.com";
    private ILogger logger = NullLogger.Instance;
    private IZenmanageCache? customCache;
    private Func<HttpClient>? httpClientFactory;
    private RuntimeEnvironment runtimeEnvironment = RuntimeEnvironment.Server;
    private string? clientAgent;

    private ConfigBuilder()
    {
    }

    /// <summary>
    /// Creates a new empty builder.
    /// </summary>
    public static ConfigBuilder Create() => new();

    /// <summary>
    /// Creates a builder from supported environment variables.
    /// </summary>
    public static ConfigBuilder FromEnvironment()
    {
        var builder = new ConfigBuilder();

        var token = Environment.GetEnvironmentVariable("ZENMANAGE_ENVIRONMENT_TOKEN");
        if (!string.IsNullOrWhiteSpace(token))
        {
            builder.WithEnvironmentToken(token);
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("ZENMANAGE_CACHE_TTL"), out var ttl))
        {
            builder.WithCacheTtl(ttl);
        }

        var backend = Environment.GetEnvironmentVariable("ZENMANAGE_CACHE_BACKEND");
        if (!string.IsNullOrWhiteSpace(backend) && Enum.TryParse<CacheBackend>(backend, true, out var parsedBackend))
        {
            builder.WithCacheBackend(parsedBackend);
        }

        var directory = Environment.GetEnvironmentVariable("ZENMANAGE_CACHE_DIR");
        if (!string.IsNullOrWhiteSpace(directory))
        {
            builder.WithCacheDirectory(directory);
        }

        var reporting = Environment.GetEnvironmentVariable("ZENMANAGE_ENABLE_USAGE_REPORTING");
        if (!string.IsNullOrWhiteSpace(reporting))
        {
            if (bool.TryParse(reporting, out var parsedReporting))
            {
                builder.WithUsageReporting(parsedReporting);
            }
            else if (reporting == "1")
            {
                builder.WithUsageReporting(true);
            }
            else if (reporting == "0")
            {
                builder.WithUsageReporting(false);
            }
        }

        var endpoint = Environment.GetEnvironmentVariable("ZENMANAGE_API_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder.WithApiEndpoint(endpoint);
        }

        return builder;
    }

    public ConfigBuilder WithEnvironmentToken(string token)
    {
        environmentToken = token;
        return this;
    }

    public ConfigBuilder WithCacheTtl(int ttl)
    {
        cacheTtl = ttl;
        return this;
    }

    public ConfigBuilder WithCacheBackend(CacheBackend backend)
    {
        cacheBackend = backend;
        return this;
    }

    public ConfigBuilder WithCacheDirectory(string directory)
    {
        cacheDirectory = directory;
        return this;
    }

    public ConfigBuilder WithUsageReporting(bool enabled)
    {
        enableUsageReporting = enabled;
        return this;
    }

    public ConfigBuilder WithApiEndpoint(string endpoint)
    {
        apiEndpoint = endpoint;
        return this;
    }

    public ConfigBuilder WithLogger(ILogger customLogger)
    {
        logger = customLogger ?? NullLogger.Instance;
        return this;
    }

    public ConfigBuilder WithCache(IZenmanageCache cache)
    {
        customCache = cache;
        return this;
    }

    public ConfigBuilder WithHttpClientFactory(Func<HttpClient> factory)
    {
        httpClientFactory = factory;
        return this;
    }

    public ConfigBuilder WithRuntimeEnvironment(RuntimeEnvironment runtime)
    {
        runtimeEnvironment = runtime;
        return this;
    }

    public ConfigBuilder WithClientAgent(string agent)
    {
        clientAgent = agent;
        return this;
    }

    /// <summary>
    /// Validates and builds the configuration.
    /// </summary>
    public Config Build()
    {
        if (string.IsNullOrWhiteSpace(environmentToken))
        {
            throw new ConfigurationException("Environment token is required");
        }

        ValidateEnvironmentToken(environmentToken, runtimeEnvironment);

        if (cacheBackend == CacheBackend.Filesystem && customCache is null && string.IsNullOrWhiteSpace(cacheDirectory))
        {
            throw new ConfigurationException("Cache directory is required for filesystem cache");
        }

        if (cacheTtl < 0)
        {
            throw new ConfigurationException("Cache TTL must be greater than or equal to 0");
        }

        return new Config(
            EnvironmentToken: environmentToken,
            CacheTtl: cacheTtl,
            CacheBackend: cacheBackend,
            CacheDirectory: cacheDirectory,
            EnableUsageReporting: enableUsageReporting,
            ApiEndpoint: apiEndpoint,
            Logger: logger,
            CustomCache: customCache,
            HttpClientFactory: httpClientFactory,
            RuntimeEnvironment: runtimeEnvironment,
            ClientAgent: clientAgent);
    }

    private static void ValidateEnvironmentToken(string token, RuntimeEnvironment runtime)
    {
        var keyType = DetectKeyType(token);

        if (keyType == KeyType.Unknown)
        {
            throw new ConfigurationException(
                $"Invalid environment token format. Expected one of: {ServerKeyPrefix}, {ClientKeyPrefix}, or {MobileKeyPrefix}.");
        }

        if (runtime == RuntimeEnvironment.Server && keyType != KeyType.Server)
        {
            throw new ConfigurationException(
                $"Invalid environment token for server runtime: {DescribeKeyType(keyType)} provided. Use a server key ({ServerKeyPrefix}...).");
        }

        if (runtime == RuntimeEnvironment.Client && keyType != KeyType.Client)
        {
            throw new ConfigurationException(
                $"Invalid environment token for client runtime: {DescribeKeyType(keyType)} provided. Use a client key ({ClientKeyPrefix}...).");
        }
    }

    private static KeyType DetectKeyType(string token)
    {
        if (token.StartsWith(ServerKeyPrefix, StringComparison.Ordinal))
        {
            return KeyType.Server;
        }

        if (token.StartsWith(ClientKeyPrefix, StringComparison.Ordinal))
        {
            return KeyType.Client;
        }

        if (token.StartsWith(MobileKeyPrefix, StringComparison.Ordinal))
        {
            return KeyType.Mobile;
        }

        return KeyType.Unknown;
    }

    private static string DescribeKeyType(KeyType keyType) => keyType switch
    {
        KeyType.Server => "server key",
        KeyType.Client => "client key",
        KeyType.Mobile => "mobile key",
        _ => "unknown key"
    };

    private enum KeyType
    {
        Unknown,
        Server,
        Client,
        Mobile
    }
}