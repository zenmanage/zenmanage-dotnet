using Zenmanage.Caching;
using Zenmanage.Exceptions;

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

    [Fact]
    public void FileSystemCache_Constructor_ThrowsCacheException_WhenDirectoryCannotBeCreated()
    {
        // Use a path whose parent segment is a regular file, so CreateDirectory must fail.
        var blockingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        File.WriteAllText(blockingFile, "not a directory");

        try
        {
            var invalidDirectory = Path.Combine(blockingFile, "cache");

            Assert.Throws<CacheException>(() => new FileSystemCache(invalidDirectory));
        }
        finally
        {
            File.Delete(blockingFile);
        }
    }

    [Fact]
    public async Task FileSystemCache_GetAsync_ReturnsNull_ForCorruptedCacheFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var cache = new FileSystemCache(directory);

        try
        {
            await cache.SetAsync("rules", "payload", 60);

            // Corrupt the cache file on disk.
            var path = Directory.EnumerateFiles(directory).Single();
            await File.WriteAllTextAsync(path, "{ not valid json");

            Assert.Null(await cache.GetAsync("rules"));
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