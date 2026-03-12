using TaskFlow.Domain.Entities;

namespace TaskFlow.Domain.Events;

public record TaskCompletedEvent(
    int TaskId, int TenantId, int UserId, string Title, DateTime CompletedAt
) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record TaskCancelledEvent(
    int TaskId, int TenantId, string Title
) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

public record TaskAssignedEvent(
    int TaskId, int TenantId, string Title, int AssignedToUserId
) : IDomainEvent
{
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
