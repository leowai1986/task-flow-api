using Microsoft.Extensions.Caching.Distributed;
using StackExchange.Redis;
using System.Text.Json;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Infrastructure.Cache;

public class RedisCacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;

    public RedisCacheService(IDistributedCache cache, IConnectionMultiplexer redis)
    {
        _cache = cache;
        _redis = redis;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        var data = await _cache.GetStringAsync(key);
        return data is null ? default : JsonSerializer.Deserialize<T>(data);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(10)
        };
        await _cache.SetStringAsync(key, JsonSerializer.Serialize(value), options);
    }

    public async Task RemoveAsync(string key) => await _cache.RemoveAsync(key);

    public async Task RemoveByPatternAsync(string pattern)
    {
        var server = _redis.GetServer(_redis.GetEndPoints().First());
        var keys = server.Keys(pattern: $"*{pattern}*").ToArray();
        if (keys.Any())
        {
            var db = _redis.GetDatabase();
            await db.KeyDeleteAsync(keys);
        }
    }
}
