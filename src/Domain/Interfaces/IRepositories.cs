using TaskFlow.Domain.Entities;

namespace TaskFlow.Domain.Interfaces;

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(int id, int tenantId);
    Task<TaskItem?> GetByIdWithDetailsAsync(int id, int tenantId);
    Task<(IEnumerable<TaskItem> Items, int TotalCount)> GetPagedAsync(TaskQuery query);
    Task<TaskItem> AddAsync(TaskItem task);
    Task UpdateAsync(TaskItem task);
    Task SoftDeleteAsync(TaskItem task, int deletedByUserId);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(int id, int tenantId);
    Task<User?> GetByEmailAsync(string email, int tenantId);
    Task<User> AddAsync(User user);
    Task<bool> ExistsAsync(string email, int tenantId);
}

public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(int id);
    Task<Tenant?> GetBySlugAsync(string slug);
    Task<Tenant> AddAsync(Tenant tenant);
    Task<bool> SlugExistsAsync(string slug);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<RefreshToken> AddAsync(RefreshToken token);
    Task UpdateAsync(RefreshToken token);
    Task RevokeAllForUserAsync(int userId, int tenantId);
}

public interface IAuditRepository
{
    Task AddAsync(AuditLog log);
}

public interface IOutboxRepository
{
    Task AddAsync(OutboxMessage message);
    Task<IEnumerable<OutboxMessage>> GetUnprocessedAsync(int batchSize = 20);
    Task UpdateAsync(OutboxMessage message);
}

public class TaskQuery
{
    public int TenantId { get; set; }
    public string? Search { get; set; }
    public Entities.TaskStatus? Status { get; set; }
    public TaskPriority? Priority { get; set; }
    public int? AssignedToUserId { get; set; }
    public DateTime? DueBefore { get; set; }
    public string? Tag { get; set; }
    public string SortBy { get; set; } = "CreatedAt";
    public bool SortDescending { get; set; } = true;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
