using Microsoft.Extensions.Logging.Abstractions;
using Zenmanage.Caching;
using Zenmanage.Flagging;

namespace Zenmanage.Tests;

public sealed class ZenmanageTests
{
    [Fact]
    public void Flags_ReturnsFlagManager()
    {
        var sdk = new global::Zenmanage.Zenmanage(ConfigBuilder.Create().WithEnvironmentToken("srv_test").Build());

        Assert.IsAssignableFrom<IFlagManager>(sdk.Flags());
    }

    [Fact]
    public async Task CustomCache_IsUsedWhenProvided()
    {
        var cache = new InMemoryCache();
        await cache.SetAsync("zenmanage_rules", "{\"version\":\"1\",\"flags\":[]}", 60);

        var sdk = new global::Zenmanage.Zenmanage(
            ConfigBuilder.Create()
                .WithEnvironmentToken("srv_test")
                .WithCache(cache)
                .WithLogger(NullLogger.Instance)
                .Build());

        var flags = await sdk.Flags().AllAsync();

        Assert.Empty(flags);
    }
}