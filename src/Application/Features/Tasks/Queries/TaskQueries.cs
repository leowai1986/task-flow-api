using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Features.Tasks.Queries;

// ── Get Paged Tasks ───────────────────────────────────────────────────────────

public record GetTasksQuery(
    string? Search, string? Status, string? Priority,
    int? AssignedToUserId, DateTime? DueBefore, string? Tag,
    string SortBy = "CreatedAt", bool SortDescending = true,
    int Page = 1, int PageSize = 20
) : IRequest<PagedResult<TaskDto>>;

public class GetTasksHandler : IRequestHandler<GetTasksQuery, PagedResult<TaskDto>>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetTasksHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<PagedResult<TaskDto>> Handle(GetTasksQuery query, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();

        var cacheKey = $"tasks:{user.TenantId}:{query.Status}:{query.Priority}:{query.Search}:{query.Page}:{query.PageSize}:{query.SortBy}:{query.SortDescending}";
        var cached = await _cache.GetAsync<PagedResult<TaskDto>>(cacheKey);
        if (cached is not null) return cached;

        var taskQuery = new TaskQuery
        {
            TenantId = user.TenantId,
            Search = query.Search,
            Status = query.Status is null ? null : Enum.Parse<Domain.Entities.TaskStatus>(query.Status, true),
            Priority = query.Priority is null ? null : Enum.Parse<TaskPriority>(query.Priority, true),
            AssignedToUserId = query.AssignedToUserId,
            DueBefore = query.DueBefore,
            Tag = query.Tag,
            SortBy = query.SortBy,
            SortDescending = query.SortDescending,
            Page = query.Page,
            PageSize = Math.Min(query.PageSize, 100)
        };

        var (items, total) = await _tasks.GetPagedAsync(taskQuery);
        var result = new PagedResult<TaskDto>(items.Select(t => t.ToDto()), total, query.Page, taskQuery.PageSize);

        await _cache.SetAsync(cacheKey, result, TimeSpan.FromMinutes(2));
        return result;
    }
}

// ── Get Task By Id ────────────────────────────────────────────────────────────

public record GetTaskByIdQuery(int Id) : IRequest<TaskDto>;

public class GetTaskByIdHandler : IRequestHandler<GetTaskByIdQuery, TaskDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;
    private readonly ICacheService _cache;

    public GetTaskByIdHandler(ITaskRepository tasks, ICurrentUserService currentUser, ICacheService cache)
    {
        _tasks = tasks; _currentUser = currentUser; _cache = cache;
    }

    public async Task<TaskDto> Handle(GetTaskByIdQuery query, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();
        var cacheKey = $"task:{user.TenantId}:{query.Id}";

        var cached = await _cache.GetAsync<TaskDto>(cacheKey);
        if (cached is not null) return cached;

        var task = await _tasks.GetByIdWithDetailsAsync(query.Id, user.TenantId)
            ?? throw new NotFoundException(nameof(TaskItem), query.Id);

        var dto = task.ToDto(includeComments: true);
        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(5));
        return dto;
    }
}
