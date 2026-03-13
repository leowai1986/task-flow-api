using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Features.Comments.Commands;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Features.Tasks.Commands;

public record TaskDto(
    int Id, int TenantId, string Title, string? Description,
    string Status, string Priority, DateTime? DueDate,
    DateTime? CompletedAt, DateTime CreatedAt, DateTime? UpdatedAt,
    string[] Tags, int CreatedByUserId, int? AssignedToUserId,
    string? AssignedToName, int CommentCount,
    IEnumerable<CommentDto>? Comments
);

// ── Create Task ───────────────────────────────────────────────────────────────

public record CreateTaskCommand(
    string Title, string? Description,
    string Priority, DateTime? DueDate, string[]? Tags
) : IRequest<TaskDto>;

public class CreateTaskValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000).When(x => x.Description != null);
        RuleFor(x => x.Priority).Must(p => Enum.TryParse<TaskPriority>(p, true, out _))
            .WithMessage("Priority must be Low, Medium, High or Critical.");
    }
}

public class CreateTaskHandler : IRequestHandler<CreateTaskCommand, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public CreateTaskHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(CreateTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var priority = Enum.Parse<TaskPriority>(cmd.Priority, true);

        var task = TaskItem.Create(user.TenantId, user.Id, cmd.Title,
            cmd.Description, priority, cmd.DueDate, cmd.Tags);

        await _tasks.AddAsync(task);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");

        return task.ToDto();
    }
}

// ── Update Task ───────────────────────────────────────────────────────────────

public record UpdateTaskCommand(
    int Id, string Title, string? Description,
    string Priority, DateTime? DueDate, string[]? Tags
) : IRequest<TaskDto>;

public class UpdateTaskHandler : IRequestHandler<UpdateTaskCommand, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public UpdateTaskHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(UpdateTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdAsync(cmd.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.Id);

        if (task.CreatedByUserId != user.Id && user.Role != "Admin")
            throw new UnauthorizedException("You can only edit your own tasks.");

        var priority = Enum.Parse<TaskPriority>(cmd.Priority, true);
        task.Update(cmd.Title, cmd.Description, priority, cmd.DueDate, cmd.Tags);
        await _tasks.UpdateAsync(task);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");
        await _cache.RemoveAsync($"task:{user.TenantId}:{cmd.Id}");

        return task.ToDto();
    }
}

// ── Transition Commands ───────────────────────────────────────────────────────

public record StartTaskCommand(int Id) : IRequest<TaskDto>;
public record CompleteTaskCommand(int Id) : IRequest<TaskDto>;
public record CancelTaskCommand(int Id) : IRequest<TaskDto>;
public record AssignTaskCommand(int Id, int AssignToUserId) : IRequest<TaskDto>;
public record DeleteTaskCommand(int Id) : IRequest<Unit>;

public class StartTaskHandler : IRequestHandler<StartTaskCommand, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public StartTaskHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(StartTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdAsync(cmd.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.Id);

        task.Start();
        await _tasks.UpdateAsync(task);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");
        return task.ToDto();
    }
}

public class CompleteTaskHandler : IRequestHandler<CompleteTaskCommand, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public CompleteTaskHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(CompleteTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdAsync(cmd.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.Id);

        task.Complete();
        await _tasks.UpdateAsync(task);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");
        return task.ToDto();
    }
}

public class CancelTaskHandler : IRequestHandler<CancelTaskCommand, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public CancelTaskHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(CancelTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdAsync(cmd.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.Id);

        task.Cancel();
        await _tasks.UpdateAsync(task);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");
        return task.ToDto();
    }
}

public class AssignTaskHandler : IRequestHandler<AssignTaskCommand, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly IUserRepository _users;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public AssignTaskHandler(ITaskRepository tasks, IUserRepository users,
        ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _users = users; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(AssignTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdAsync(cmd.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.Id);

        var assignee = await _users.GetByIdAsync(cmd.AssignToUserId, user.TenantId)
            ?? throw new NotFoundException("User", cmd.AssignToUserId);

        task.Assign(assignee.Id);
        await _tasks.UpdateAsync(task);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");
        return task.ToDto();
    }
}

public class DeleteTaskHandler : IRequestHandler<DeleteTaskCommand, Unit>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public DeleteTaskHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<Unit> Handle(DeleteTaskCommand cmd, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var task = await _tasks.GetByIdAsync(cmd.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), cmd.Id);

        if (task.CreatedByUserId != user.Id && user.Role != "Admin")
            throw new UnauthorizedException("You can only delete your own tasks.");

        await _tasks.SoftDeleteAsync(task, user.Id);
        await _cache.RemoveByPatternAsync($"tasks:{user.TenantId}:*");
        await _cache.RemoveAsync($"task:{user.TenantId}:{cmd.Id}");
        return Unit.Value;
    }
}

// ── Mapping helper ────────────────────────────────────────────────────────────
public static class TaskMappingExtensions
{
    public static TaskDto ToDto(this TaskItem t, bool includeComments = false) =>
        new(t.Id, t.TenantId, t.Title, t.Description,
            t.Status.ToString(), t.Priority.ToString(),
            t.DueDate, t.CompletedAt, t.CreatedAt, t.UpdatedAt,
            t.Tags, t.CreatedByUserId, t.AssignedToUserId,
            t.AssignedTo?.FullName, t.Comments.Count,
            includeComments ? t.Comments.Select(c => new CommentDto(
                c.Id, c.TaskItemId, c.UserId, c.User?.FullName ?? "",
                c.Content, c.IsEdited, c.CreatedAt, c.UpdatedAt)) : null
    );
}
