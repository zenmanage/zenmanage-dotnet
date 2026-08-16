using Zenmanage;
using Zenmanage.Caching;

var memorySdk = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .WithCacheBackend(CacheBackend.Memory)
        .WithCacheTtl(900)
        .Build());

var filesystemSdk = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .WithCacheBackend(CacheBackend.Filesystem)
        .WithCacheDirectory(Path.Combine(AppContext.BaseDirectory, ".zenmanage-cache"))
        .Build());

var nullCacheSdk = new Zenmanage.Zenmanage(
    ConfigBuilder.Create()
        .WithEnvironmentToken("srv_your_server_key_here")
        .WithCacheBackend(CacheBackend.Null)
        .Build());

_ = await memorySdk.Flags().AllAsync();
_ = await filesystemSdk.Flags().AllAsync();
_ = await nullCacheSdk.Flags().AllAsync();

Console.WriteLine("Loaded rules using memory, filesystem, and null caches.");