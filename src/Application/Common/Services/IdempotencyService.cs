using System.Text.Json;
using TaskFlow.Application.Common.Interfaces;

namespace TaskFlow.Application.Common.Services;

public class IdempotencyService : IIdempotencyService
{
    private readonly ICacheService _cache;
    private static readonly TimeSpan Expiry = TimeSpan.FromHours(24);

    public IdempotencyService(ICacheService cache) => _cache = cache;

    private static string Key(string idempotencyKey) => $"idempotency:{idempotencyKey}";

    public async Task<bool> IsRequestProcessedAsync(string idempotencyKey) =>
        await _cache.GetAsync<string>(Key(idempotencyKey)) is not null;

    public async Task MarkRequestProcessedAsync(string idempotencyKey, object result) =>
        await _cache.SetAsync(Key(idempotencyKey), JsonSerializer.Serialize(result), Expiry);

    public async Task<T?> GetCachedResultAsync<T>(string idempotencyKey)
    {
        var raw = await _cache.GetAsync<string>(Key(idempotencyKey));
        return raw is null ? default : JsonSerializer.Deserialize<T>(raw);
    }
}
