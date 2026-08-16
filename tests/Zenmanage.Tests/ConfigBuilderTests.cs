using Zenmanage.Caching;
using Zenmanage.Exceptions;

namespace Zenmanage.Tests;

public sealed class ConfigBuilderTests
{
    [Fact]
    public void Build_Throws_WhenEnvironmentTokenIsMissing()
    {
        var builder = ConfigBuilder.Create();

        var exception = Assert.Throws<ConfigurationException>(() => builder.Build());

        Assert.Equal("Environment token is required", exception.Message);
    }

    [Fact]
    public void Build_UsesExpectedDefaults()
    {
        var config = ConfigBuilder.Create()
            .WithEnvironmentToken("srv_test_123")
            .Build();

        Assert.Equal("srv_test_123", config.EnvironmentToken);
        Assert.Equal(3600, config.CacheTtl);
        Assert.Equal(CacheBackend.Memory, config.CacheBackend);
        Assert.True(config.EnableUsageReporting);
        Assert.Equal("https://api.zenmanage.com", config.ApiEndpoint);
        Assert.Equal(RuntimeEnvironment.Server, config.RuntimeEnvironment);
        Assert.NotNull(config.Logger);
    }

    [Fact]
    public void Build_RequiresDirectory_ForFilesystemCache()
    {
        var builder = ConfigBuilder.Create()
            .WithEnvironmentToken("srv_test_123")
            .WithCacheBackend(CacheBackend.Filesystem);

        var exception = Assert.Throws<ConfigurationException>(() => builder.Build());

        Assert.Equal("Cache directory is required for filesystem cache", exception.Message);
    }

    [Fact]
    public void Build_RejectsClientKey_ForServerRuntime()
    {
        var builder = ConfigBuilder.Create().WithEnvironmentToken("cli_test_123");

        var exception = Assert.Throws<ConfigurationException>(() => builder.Build());

        Assert.Contains("Invalid environment token for server runtime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Build_AcceptsClientKey_ForClientRuntime()
    {
        var config = ConfigBuilder.Create()
            .WithEnvironmentToken("cli_test_123")
            .WithRuntimeEnvironment(RuntimeEnvironment.Client)
            .Build();

        Assert.Equal("cli_test_123", config.EnvironmentToken);
    }

    [Fact]
    public void FromEnvironment_ReadsSupportedVariables()
    {
        Environment.SetEnvironmentVariable("ZENMANAGE_ENVIRONMENT_TOKEN", "srv_env_test");
        Environment.SetEnvironmentVariable("ZENMANAGE_CACHE_TTL", "1800");
        Environment.SetEnvironmentVariable("ZENMANAGE_CACHE_BACKEND", "Null");
        Environment.SetEnvironmentVariable("ZENMANAGE_ENABLE_USAGE_REPORTING", "false");
        Environment.SetEnvironmentVariable("ZENMANAGE_API_ENDPOINT", "https://env.api.example");

        try
        {
            var config = ConfigBuilder.FromEnvironment().Build();

            Assert.Equal("srv_env_test", config.EnvironmentToken);
            Assert.Equal(1800, config.CacheTtl);
            Assert.Equal(CacheBackend.Null, config.CacheBackend);
            Assert.False(config.EnableUsageReporting);
            Assert.Equal("https://env.api.example", config.ApiEndpoint);
        }
        finally
        {
            Environment.SetEnvironmentVariable("ZENMANAGE_ENVIRONMENT_TOKEN", null);
            Environment.SetEnvironmentVariable("ZENMANAGE_CACHE_TTL", null);
            Environment.SetEnvironmentVariable("ZENMANAGE_CACHE_BACKEND", null);
            Environment.SetEnvironmentVariable("ZENMANAGE_ENABLE_USAGE_REPORTING", null);
            Environment.SetEnvironmentVariable("ZENMANAGE_API_ENDPOINT", null);
        }
    }
}