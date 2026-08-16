using System.Collections.Concurrent;

namespace Zenmanage.Caching;

/// <summary>
/// In-memory cache suitable for most applications and serverless workloads.
/// </summary>
public sealed class InMemoryCache : IZenmanageCache
{
    private readonly ConcurrentDictionary<string, CacheItem> cache = new();

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!cache.TryGetValue(key, out var item))
        {
            return Task.FromResult<string?>(null);
        }

        if (item.IsExpired(DateTimeOffset.UtcNow))
        {
            cache.TryRemove(key, out _);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult<string?>(item.Value);
    }

    public Task SetAsync(string key, string value, int ttlSeconds, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset? expiresAt = ttlSeconds <= 0 ? null : DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);
        cache[key] = new CacheItem(value, expiresAt);
        return Task.CompletedTask;
    }

    public async Task<bool> HasAsync(string key, CancellationToken cancellationToken = default)
        => await GetAsync(key, cancellationToken).ConfigureAwait(false) is not null;

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        cache.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        cache.Clear();
        return Task.CompletedTask;
    }
}