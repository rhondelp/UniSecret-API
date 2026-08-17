using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;

namespace UniSecretApi.Services;

public class CacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IDistributedCache? _distributedCache;

    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);

    public CacheService(
        IMemoryCache memoryCache,
        IDistributedCache? distributedCache = null)
    {
        _memoryCache = memoryCache;
        _distributedCache = distributedCache;
    }

    public async Task<T?> GetAsync<T>(
        string key,
        CancellationToken cancellationToken = default)
    {
        // --------------------------------------------------------
        // Level 1: Local memory cache
        // --------------------------------------------------------

        if (_memoryCache.TryGetValue(key, out T? memoryValue))
        {
            return memoryValue;
        }

        // --------------------------------------------------------
        // Level 2: Distributed Redis cache
        // --------------------------------------------------------

        if (_distributedCache is null)
        {
            return default;
        }

        var cachedJson =
            await _distributedCache.GetStringAsync(
                key,
                cancellationToken);

        if (string.IsNullOrWhiteSpace(cachedJson))
        {
            return default;
        }

        var value =
            JsonSerializer.Deserialize<T>(
                cachedJson,
                JsonOptions);

        if (value is not null)
        {
            // Populate local memory cache after Redis hit.
            _memoryCache.Set(
                key,
                value,
                TimeSpan.FromSeconds(30));
        }

        return value;
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan distributedExpiration,
        TimeSpan memoryExpiration,
        CancellationToken cancellationToken = default)
    {
        // --------------------------------------------------------
        // Local cache
        // --------------------------------------------------------

        _memoryCache.Set(
            key,
            value,
            memoryExpiration);

        // --------------------------------------------------------
        // Distributed cache
        // --------------------------------------------------------

        if (_distributedCache is null)
        {
            return;
        }

        var json =
            JsonSerializer.Serialize(
                value,
                JsonOptions);

        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow =
                distributedExpiration
        };

        await _distributedCache.SetStringAsync(
            key,
            json,
            options,
            cancellationToken);
    }

    public async Task RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _memoryCache.Remove(key);

        if (_distributedCache is not null)
        {
            await _distributedCache.RemoveAsync(
                key,
                cancellationToken);
        }
    }
}