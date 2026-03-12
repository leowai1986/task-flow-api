namespace TaskFlow.Domain.Entities;

public abstract class BaseEntity
{
    public int Id { get; protected set; }
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    // Soft delete
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public int? DeletedByUserId { get; private set; }

    private readonly List<IDomainEvent> _domainEvents = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();
    protected void SetUpdated() => UpdatedAt = DateTime.UtcNow;

    public void SoftDelete(int deletedByUserId)
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedByUserId = deletedByUserId;
    }
}

public interface IDomainEvent
{
    DateTime OccurredAt { get; }
}
