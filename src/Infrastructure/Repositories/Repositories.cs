using Microsoft.EntityFrameworkCore;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Data;

namespace TaskFlow.Infrastructure.Repositories;

public class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _db;
    public TaskRepository(AppDbContext db) => _db = db;

    public async Task<TaskItem?> GetByIdAsync(int id, int tenantId) =>
        await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

    public async Task<TaskItem?> GetByIdWithDetailsAsync(int id, int tenantId) =>
        await _db.Tasks
            .Include(t => t.CreatedBy)
            .Include(t => t.AssignedTo)
            .Include(t => t.Comments).ThenInclude(c => c.User)
            .FirstOrDefaultAsync(t => t.Id == id && t.TenantId == tenantId);

    public async Task<(IEnumerable<TaskItem> Items, int TotalCount)> GetPagedAsync(TaskQuery query)
    {
        var q = _db.Tasks
            .Include(t => t.AssignedTo)
            .Where(t => t.TenantId == query.TenantId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
            q = q.Where(t => t.Title.Contains(query.Search) ||
                              (t.Description != null && t.Description.Contains(query.Search)));

        if (query.Status.HasValue) q = q.Where(t => t.Status == query.Status.Value);
        if (query.Priority.HasValue) q = q.Where(t => t.Priority == query.Priority.Value);
        if (query.AssignedToUserId.HasValue) q = q.Where(t => t.AssignedToUserId == query.AssignedToUserId.Value);
        if (query.DueBefore.HasValue) q = q.Where(t => t.DueDate <= query.DueBefore.Value);
        if (!string.IsNullOrWhiteSpace(query.Tag)) q = q.Where(t => t.Tags.Contains(query.Tag));

        var total = await q.CountAsync();

        q = query.SortBy.ToLower() switch
        {
            "title" => query.SortDescending ? q.OrderByDescending(t => t.Title) : q.OrderBy(t => t.Title),
            "priority" => query.SortDescending ? q.OrderByDescending(t => t.Priority) : q.OrderBy(t => t.Priority),
            "duedate" => query.SortDescending ? q.OrderByDescending(t => t.DueDate) : q.OrderBy(t => t.DueDate),
            "status" => query.SortDescending ? q.OrderByDescending(t => t.Status) : q.OrderBy(t => t.Status),
            _ => query.SortDescending ? q.OrderByDescending(t => t.CreatedAt) : q.OrderBy(t => t.CreatedAt)
        };

        var items = await q.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToListAsync();
        return (items, total);
    }

    public async Task<TaskItem> AddAsync(TaskItem task) { _db.Tasks.Add(task); await _db.SaveChangesAsync(); return task; }
    public async Task UpdateAsync(TaskItem task) { _db.Tasks.Update(task); await _db.SaveChangesAsync(); }
    public async Task SoftDeleteAsync(TaskItem task, int deletedByUserId) { task.SoftDelete(deletedByUserId); _db.Tasks.Update(task); await _db.SaveChangesAsync(); }
}

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;
    public UserRepository(AppDbContext db) => _db = db;

    public async Task<User?> GetByIdAsync(int id, int tenantId) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == tenantId);

    public async Task<User?> GetByEmailAsync(string email, int tenantId) =>
        await _db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant() && u.TenantId == tenantId);

    public async Task<bool> ExistsAsync(string email, int tenantId) =>
        await _db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant() && u.TenantId == tenantId);

    public async Task<User> AddAsync(User user) { _db.Users.Add(user); await _db.SaveChangesAsync(); return user; }
}

public class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;
    public TenantRepository(AppDbContext db) => _db = db;

    public async Task<Tenant?> GetByIdAsync(int id) => await _db.Tenants.FindAsync(id);
    public async Task<Tenant?> GetBySlugAsync(string slug) => await _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug);
    public async Task<bool> SlugExistsAsync(string slug) => await _db.Tenants.AnyAsync(t => t.Slug == slug);
    public async Task<Tenant> AddAsync(Tenant tenant) { _db.Tenants.Add(tenant); await _db.SaveChangesAsync(); return tenant; }
}
