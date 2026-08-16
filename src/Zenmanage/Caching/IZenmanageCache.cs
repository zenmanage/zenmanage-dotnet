namespace Zenmanage.Caching;

/// <summary>
/// Abstraction for rule caching.
/// </summary>
public interface IZenmanageCache
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    Task SetAsync(string key, string value, int ttlSeconds, CancellationToken cancellationToken = default);

    Task<bool> HasAsync(string key, CancellationToken cancellationToken = default);

    Task DeleteAsync(string key, CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}