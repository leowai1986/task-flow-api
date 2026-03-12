using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Domain.Entities;

public class TaskComment : BaseEntity
{
    public int TenantId { get; private set; }
    public int TaskItemId { get; private set; }
    public int UserId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public bool IsEdited { get; private set; }

    public User User { get; private set; } = null!;
    public TaskItem TaskItem { get; private set; } = null!;

    private TaskComment() { }

    public static TaskComment Create(int tenantId, int taskItemId, int userId, string content)
    {
        if (string.IsNullOrWhiteSpace(content)) throw new DomainException("Comment content is required.");
        if (content.Length > 2000) throw new DomainException("Comment cannot exceed 2000 characters.");

        return new TaskComment
        {
            TenantId = tenantId,
            TaskItemId = taskItemId,
            UserId = userId,
            Content = content
        };
    }

    public void Edit(string content, int requestingUserId)
    {
        if (UserId != requestingUserId) throw new DomainException("You can only edit your own comments.");
        if (string.IsNullOrWhiteSpace(content)) throw new DomainException("Comment content is required.");

        Content = content;
        IsEdited = true;
        SetUpdated();
    }
}
