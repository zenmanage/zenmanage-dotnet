using Zenmanage.Caching;

namespace Zenmanage.Tests;

public sealed class CacheTests
{
    [Fact]
    public async Task InMemoryCache_RoundTripsValues()
    {
        var cache = new InMemoryCache();

        await cache.SetAsync("rules", "payload", 60);

        var value = await cache.GetAsync("rules");

        Assert.Equal("payload", value);
        Assert.True(await cache.HasAsync("rules"));
    }

    [Fact]
    public async Task NullCache_NeverStoresValues()
    {
        var cache = new NullCache();

        await cache.SetAsync("rules", "payload", 60);

        Assert.Null(await cache.GetAsync("rules"));
        Assert.False(await cache.HasAsync("rules"));
    }

    [Fact]
    public async Task FileSystemCache_RoundTripsValues()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cache = new FileSystemCache(directory);

        try
        {
            await cache.SetAsync("rules", "payload", 60);

            var value = await cache.GetAsync("rules");

            Assert.Equal("payload", value);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}