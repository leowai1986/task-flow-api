using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Features.Metrics.Queries;

public record TenantMetricsDto(
    int TotalTasks,
    int TodoCount,
    int InProgressCount,
    int DoneCount,
    int CancelledCount,
    int OverdueCount,
    int CriticalPriorityCount,
    double AverageCompletionHours,
    IEnumerable<TagCountDto> TopTags,
    IEnumerable<UserTaskCountDto> TasksByUser
);

public record TagCountDto(string Tag, int Count);
public record UserTaskCountDto(string UserFullName, int TaskCount, int CompletedCount);

public record GetTenantMetricsQuery : IRequest<TenantMetricsDto>;

public class GetTenantMetricsHandler : IRequestHandler<GetTenantMetricsQuery, TenantMetricsDto>
{
    private readonly ITaskRepository _tasks;
    private readonly ICurrentUserService _currentUser;

    public GetTenantMetricsHandler(ITaskRepository tasks, ICurrentUserService currentUser)
    {
        _tasks = tasks;
        _currentUser = currentUser;
    }

    public async Task<TenantMetricsDto> Handle(GetTenantMetricsQuery request, CancellationToken ct)
    {
        var user = _currentUser.User ?? throw new UnauthorizedException();

        var (taskItems, _) = await _tasks.GetPagedAsync(new TaskQuery
        {
            TenantId = user.TenantId,
            PageSize = 10000
        });
        var tasks = taskItems.ToList();

        var now = DateTime.UtcNow;

        var totalTasks = tasks.Count;
        var todoCount = tasks.Count(t => t.Status == Domain.Entities.TaskStatus.Todo);
        var inProgressCount = tasks.Count(t => t.Status == Domain.Entities.TaskStatus.InProgress);
        var doneCount = tasks.Count(t => t.Status == Domain.Entities.TaskStatus.Done);
        var cancelledCount = tasks.Count(t => t.Status == Domain.Entities.TaskStatus.Cancelled);
        var overdueCount = tasks.Count(t => t.DueDate < now && t.Status != Domain.Entities.TaskStatus.Done && t.Status != Domain.Entities.TaskStatus.Cancelled);
        var criticalCount = tasks.Count(t => t.Priority == TaskPriority.Critical);

        var completedTasks = tasks.Where(t => t.CompletedAt.HasValue).ToList();
        var avgCompletionHours = completedTasks.Any()
            ? completedTasks.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalHours)
            : 0;

        // Top tags
        var topTags = tasks
            .SelectMany(t => t.Tags)
            .GroupBy(tag => tag)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => new TagCountDto(g.Key, g.Count()))
            .ToList();

        // Tasks by user
        var tasksByUser = tasks
            .GroupBy(t => t.CreatedBy.FullName)
            .Select(g => new UserTaskCountDto(
                g.Key,
                g.Count(),
                g.Count(t => t.Status == Domain.Entities.TaskStatus.Done)
            ))
            .OrderByDescending(u => u.TaskCount)
            .ToList();

        return new TenantMetricsDto(
            totalTasks, todoCount, inProgressCount, doneCount, cancelledCount,
            overdueCount, criticalCount, Math.Round(avgCompletionHours, 1),
            topTags, tasksByUser
        );
    }
}
