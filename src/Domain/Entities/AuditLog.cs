namespace TaskFlow.Domain.Entities;

public class AuditLog
{
    public long Id { get; private set; }
    public int TenantId { get; private set; }
    public int? UserId { get; private set; }
    public string UserEmail { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;      // e.g. "Task.Created"
    public string EntityName { get; private set; } = string.Empty;  // e.g. "TaskItem"
    public int? EntityId { get; private set; }
    public string? OldValues { get; private set; }
    public string? NewValues { get; private set; }
    public string? IpAddress { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(int tenantId, int? userId, string userEmail,
        string action, string entityName, int? entityId = null,
        string? oldValues = null, string? newValues = null, string? ipAddress = null)
    {
        return new AuditLog
        {
            TenantId = tenantId,
            UserId = userId,
            UserEmail = userEmail,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            IpAddress = ipAddress,
            OccurredAt = DateTime.UtcNow
        };
    }
}
