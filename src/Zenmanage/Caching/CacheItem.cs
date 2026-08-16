namespace Zenmanage.Caching;

internal sealed record CacheItem(string Value, DateTimeOffset? ExpiresAt)
{
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is not null && ExpiresAt <= now;
}