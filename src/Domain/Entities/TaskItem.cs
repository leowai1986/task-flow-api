using TaskFlow.Domain.Events;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public enum TaskPriority { Low, Medium, High, Critical }
public enum TaskStatus { Todo, InProgress, Done, Cancelled }

public class TaskItem : BaseEntity
{
    public int TenantId { get; private set; }
    public int CreatedByUserId { get; private set; }
    public int? AssignedToUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public TaskStatus Status { get; private set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; private set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public string[] Tags { get; private set; } = Array.Empty<string>();

    public User CreatedBy { get; private set; } = null!;
    public User? AssignedTo { get; private set; }

    private readonly List<TaskComment> _comments = new();
    public IReadOnlyCollection<TaskComment> Comments => _comments.AsReadOnly();

    private TaskItem() { }

    public static TaskItem Create(int tenantId, int createdByUserId, string title,
        string? description, TaskPriority priority, DateTime? dueDate, string[]? tags = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow.Date)
            throw new DomainException("Due date cannot be in the past.");

        return new TaskItem
        {
            TenantId = tenantId,
            CreatedByUserId = createdByUserId,
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = dueDate,
            Tags = tags ?? Array.Empty<string>()
        };
    }

    public void Start()
    {
        if (Status != TaskStatus.Todo)
            throw new DomainException("Only Todo tasks can be started.");
        Status = TaskStatus.InProgress;
        SetUpdated();
    }

    public void Complete()
    {
        if (Status == TaskStatus.Done) return;
        if (Status == TaskStatus.Cancelled)
            throw new DomainException("Cannot complete a cancelled task.");

        Status = TaskStatus.Done;
        CompletedAt = DateTime.UtcNow;
        SetUpdated();

        AddDomainEvent(new TaskCompletedEvent(Id, TenantId, CreatedByUserId, Title, CompletedAt.Value));
    }

    public void Cancel()
    {
        if (Status == TaskStatus.Done)
            throw new DomainException("Cannot cancel a completed task.");
        Status = TaskStatus.Cancelled;
        SetUpdated();
        AddDomainEvent(new TaskCancelledEvent(Id, TenantId, Title));
    }

    public void Assign(int userId)
    {
        AssignedToUserId = userId;
        SetUpdated();
        AddDomainEvent(new TaskAssignedEvent(Id, TenantId, Title, userId));
    }

    public void Update(string title, string? description, TaskPriority priority, DateTime? dueDate, string[]? tags)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (dueDate.HasValue && dueDate.Value < DateTime.UtcNow.Date)
            throw new DomainException("Due date cannot be in the past.");

        Title = title;
        Description = description;
        Priority = priority;
        DueDate = dueDate;
        Tags = tags ?? Array.Empty<string>();
        SetUpdated();
    }

    public void AddComment(TaskComment comment) => _comments.Add(comment);
}
