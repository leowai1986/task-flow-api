using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Features.Comments.Commands;

public record CommentDto(int Id, int TaskItemId, int UserId, string UserFullName,
    string Content, bool IsEdited, DateTime CreatedAt, DateTime? UpdatedAt);

public record AddCommentCommand(int TaskId, string Content) : IRequest<CommentDto>;
public record EditCommentCommand(int CommentId, string Content) : IRequest<CommentDto>;
public record DeleteCommentCommand(int CommentId) : IRequest<Unit>;

public class AddCommentHandler : IRequestHandler<AddCommentCommand, CommentDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public AddCommentHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<CommentDto> Handle(AddCommentCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdWithDetailsAsync(cmd.TaskId, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.TaskId);

        var comment = TaskComment.Create(user.TenantId, task.Id, user.Id, cmd.Content);
        task.AddComment(comment);
        await _tasks.UpdateAsync(task);
        await _cache.RemoveAsync($"task:{user.TenantId}:{cmd.TaskId}");

        return new CommentDto(comment.Id, comment.TaskItemId, comment.UserId,
            user.Email, comment.Content, comment.IsEdited, comment.CreatedAt, comment.UpdatedAt);
    }
}
