using Microsoft.EntityFrameworkCore;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;

namespace TaskFlow.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUser;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUser)
        : base(options) => _currentUser = currentUser;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<TaskItem> Tasks => Set<TaskItem>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Name).IsRequired().HasMaxLength(100);
            e.Property(t => t.Slug).IsRequired().HasMaxLength(50);
            e.HasIndex(t => t.Slug).IsUnique();
        });

        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).IsRequired().HasMaxLength(200);
            e.Property(u => u.FullName).IsRequired().HasMaxLength(100);
            e.Property(u => u.PasswordHash).IsRequired();
            e.Property(u => u.Role).HasConversion<string>();
            e.HasIndex(u => new { u.TenantId, u.Email }).IsUnique();
            e.HasOne(u => u.Tenant).WithMany(t => t.Users).HasForeignKey(u => u.TenantId);
        });

        modelBuilder.Entity<TaskItem>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Title).IsRequired().HasMaxLength(200);
            e.Property(t => t.Description).HasMaxLength(2000);
            e.Property(t => t.Status).HasConversion<string>();
            e.Property(t => t.Priority).HasConversion<string>();
            e.Property(t => t.Tags).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            e.HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(t => t.AssignedTo).WithMany().HasForeignKey(t => t.AssignedToUserId).IsRequired(false).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(t => new { t.TenantId, t.Status });
            e.HasIndex(t => new { t.TenantId, t.Priority });
            e.HasIndex(t => new { t.TenantId, t.IsDeleted });
            e.HasQueryFilter(t => !t.IsDeleted);
        });

        modelBuilder.Entity<TaskComment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Content).IsRequired().HasMaxLength(2000);
            e.HasOne(c => c.TaskItem).WithMany(t => t.Comments).HasForeignKey(c => c.TaskItemId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(c => c.User).WithMany().HasForeignKey(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasQueryFilter(c => !c.IsDeleted);
        });

        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Token).IsRequired().HasMaxLength(500);
            e.HasIndex(r => r.Token).IsUnique();
            e.HasOne(r => r.User).WithMany().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Action).IsRequired().HasMaxLength(100);
            e.Property(a => a.EntityName).IsRequired().HasMaxLength(100);
            e.Property(a => a.UserEmail).HasMaxLength(200);
            e.HasIndex(a => new { a.TenantId, a.OccurredAt });
            e.HasIndex(a => new { a.EntityName, a.EntityId });
        });

        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.HasKey(o => o.Id);
            e.Property(o => o.Type).IsRequired().HasMaxLength(200);
            e.HasIndex(o => o.ProcessedAt);
        });
    }
}
