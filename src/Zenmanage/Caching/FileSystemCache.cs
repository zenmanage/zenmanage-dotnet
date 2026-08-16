using System.Text.Json;

namespace Zenmanage.Caching;

/// <summary>
/// File-backed cache for long-running server processes.
/// </summary>
public sealed class FileSystemCache : IZenmanageCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string directory;

    public FileSystemCache(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        this.directory = directory;
        Directory.CreateDirectory(directory);
    }

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var path = GetPath(key);
        if (!File.Exists(path))
        {
            return null;
        }

        await using var stream = File.OpenRead(path);
        var item = await JsonSerializer.DeserializeAsync<CacheItem>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        if (item is null)
        {
            return null;
        }

        if (item.IsExpired(DateTimeOffset.UtcNow))
        {
            File.Delete(path);
            return null;
        }

        return item.Value;
    }

    public async Task SetAsync(string key, string value, int ttlSeconds, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(directory);
        var path = GetPath(key);
        DateTimeOffset? expiresAt = ttlSeconds <= 0 ? null : DateTimeOffset.UtcNow.AddSeconds(ttlSeconds);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, new CacheItem(value, expiresAt), JsonOptions, cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> HasAsync(string key, CancellationToken cancellationToken = default)
        => await GetAsync(key, cancellationToken).ConfigureAwait(false) is not null;

    public Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = GetPath(key);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (Directory.Exists(directory))
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.json"))
            {
                File.Delete(file);
            }
        }

        return Task.CompletedTask;
    }

    private string GetPath(string key)
    {
        var safeName = Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(key));
        return Path.Combine(directory, safeName + ".json");
    }
}