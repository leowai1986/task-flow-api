using TaskFlow.Application.Common.Models;

namespace TaskFlow.Application.Common.Interfaces;

public interface ICurrentUserService
{
    CurrentUser? User { get; }
    bool IsAuthenticated { get; }
}

public interface IJwtService
{
    string GenerateAccessToken(int userId, int tenantId, string email, string role);
    string GenerateRefreshToken();
    CurrentUser? ValidateToken(string token);
}

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPatternAsync(string pattern);
}

public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string hash);
}

public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<TaskFlow.Domain.Entities.IDomainEvent> events);
}

public interface IAuditService
{
    Task LogAsync(string action, string entityName, int? entityId = null,
        string? oldValues = null, string? newValues = null);
}

public interface IIdempotencyService
{
    Task<bool> IsRequestProcessedAsync(string idempotencyKey);
    Task MarkRequestProcessedAsync(string idempotencyKey, object result);
    Task<T?> GetCachedResultAsync<T>(string idempotencyKey);
}
