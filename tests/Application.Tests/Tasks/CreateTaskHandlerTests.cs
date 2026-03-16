using FluentAssertions;
using Moq;
using NUnit.Framework;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Tests.Tasks;

[TestFixture]
public class CreateTaskHandlerTests
{
    private Mock<ITaskRepository> _taskRepo = null!;
    private Mock<ICurrentUserService> _currentUser = null!;
    private Mock<ICacheService> _cache = null!;
    private CreateTaskHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _taskRepo = new Mock<ITaskRepository>();
        _currentUser = new Mock<ICurrentUserService>();
        _cache = new Mock<ICacheService>();

        _currentUser.Setup(u => u.User)
            .Returns(new CurrentUser(1, 10, "leo@test.com", "Member"));

        _taskRepo.Setup(r => r.AddAsync(It.IsAny<TaskItem>()))
            .ReturnsAsync((TaskItem t) => t);

        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _handler = new CreateTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);
    }

    [Test]
    public async Task Handle_ValidCommand_CreatesTaskAndInvalidatesCache()
    {
        var cmd = new CreateTaskCommand("New Feature", "Description", "High", null, new[] { "backend" });

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Title.Should().Be("New Feature");
        result.Priority.Should().Be("High");
        result.Status.Should().Be("Todo");
        result.Tags.Should().Contain("backend");
        _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Once);
        _cache.Verify(c => c.RemoveByPatternAsync($"tasks:10:*"), Times.Once);
    }

    [Test]
    public async Task Handle_ValidCommand_UsesCurrentUserTenantId()
    {
        _currentUser.Setup(u => u.User)
            .Returns(new CurrentUser(5, 99, "other@test.com", "Member"));
        var handler = new CreateTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);

        var cmd = new CreateTaskCommand("Task", null, "Low", null, null);
        var result = await handler.Handle(cmd, CancellationToken.None);

        result.TenantId.Should().Be(99);
        _cache.Verify(c => c.RemoveByPatternAsync("tasks:99:*"), Times.Once);
    }

    [Test]
    public async Task Handle_NoAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUser.Setup(u => u.User).Returns((CurrentUser?)null);
        var handler = new CreateTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);

        Func<Task> act = () => handler.Handle(
            new CreateTaskCommand("Title", null, "Low", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Test]
    public async Task Handle_InvalidPriority_ThrowsException()
    {
        var cmd = new CreateTaskCommand("Title", null, "InvalidPriority", null, null);
        Func<Task> act = () => _handler.Handle(cmd, CancellationToken.None);
        await act.Should().ThrowAsync<Exception>();
    }

    [Test]
    public async Task Handle_NullTags_CreatesTaskWithEmptyTags()
    {
        var cmd = new CreateTaskCommand("Task", null, "Low", null, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.Tags.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_WithFutureDueDate_CreatesTask()
    {
        var due = DateTime.UtcNow.AddDays(7);
        var cmd = new CreateTaskCommand("Task", null, "Medium", due, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);
        result.DueDate.Should().Be(due);
    }
}

[TestFixture]
public class CompleteTaskHandlerTests
{
    private Mock<ITaskRepository> _taskRepo = null!;
    private Mock<ICurrentUserService> _currentUser = null!;
    private Mock<ICacheService> _cache = null!;
    private CompleteTaskHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _taskRepo = new Mock<ITaskRepository>();
        _currentUser = new Mock<ICurrentUserService>();
        _cache = new Mock<ICacheService>();

        _currentUser.Setup(u => u.User)
            .Returns(new CurrentUser(1, 10, "leo@test.com", "Member"));

        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>()))
            .Returns(Task.CompletedTask);

        _handler = new CompleteTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);
    }

    [Test]
    public async Task Handle_ValidTask_TransitionsToDone()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        var result = await _handler.Handle(new CompleteTaskCommand(1), CancellationToken.None);

        result.Status.Should().Be("Done");
        result.CompletedAt.Should().NotBeNull();
        _taskRepo.Verify(r => r.UpdateAsync(task), Times.Once);
    }

    [Test]
    public async Task Handle_TaskNotFound_ThrowsNotFoundException()
    {
        _taskRepo.Setup(r => r.GetByIdAsync(999, 10)).ReturnsAsync((TaskItem?)null);

        Func<Task> act = () => _handler.Handle(new CompleteTaskCommand(999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_CancelledTask_ThrowsDomainException()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        task.Cancel();
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        Func<Task> act = () => _handler.Handle(new CompleteTaskCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*cancelled*");
        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Test]
    public async Task Handle_NoAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUser.Setup(u => u.User).Returns((CurrentUser?)null);
        var handler = new CompleteTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);

        Func<Task> act = () => handler.Handle(new CompleteTaskCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Test]
    public async Task Handle_CompletesTask_InvalidatesBothCacheKeys()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        await _handler.Handle(new CompleteTaskCommand(1), CancellationToken.None);

        _cache.Verify(c => c.RemoveByPatternAsync("tasks:10:*"), Times.Once);
    }
}

[TestFixture]
public class DeleteTaskHandlerTests
{
    private Mock<ITaskRepository> _taskRepo = null!;
    private Mock<ICurrentUserService> _currentUser = null!;
    private Mock<ICacheService> _cache = null!;
    private DeleteTaskHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _taskRepo = new Mock<ITaskRepository>();
        _currentUser = new Mock<ICurrentUserService>();
        _cache = new Mock<ICacheService>();

        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.SoftDeleteAsync(It.IsAny<TaskItem>(), It.IsAny<int>())).Returns(Task.CompletedTask);

        _handler = new DeleteTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);
    }

    [Test]
    public async Task Handle_OwnerDeletesTask_SoftDeletesAndInvalidatesCache()
    {
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(1, 10, "owner@test.com", "Member"));
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Low, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        await _handler.Handle(new DeleteTaskCommand(1), CancellationToken.None);

        _taskRepo.Verify(r => r.SoftDeleteAsync(task, 1), Times.Once);
        _cache.Verify(c => c.RemoveByPatternAsync("tasks:10:*"), Times.Once);
        _cache.Verify(c => c.RemoveAsync("task:10:1"), Times.Once);
    }

    [Test]
    public async Task Handle_AdminDeletesOthersTask_Succeeds()
    {
        // Admin (userId=99) deletes a task created by userId=1
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(99, 10, "admin@test.com", "Admin"));
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Low, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        await _handler.Handle(new DeleteTaskCommand(1), CancellationToken.None);

        _taskRepo.Verify(r => r.SoftDeleteAsync(task, 99), Times.Once);
    }

    [Test]
    public async Task Handle_NonOwnerMemberDeletesTask_ThrowsUnauthorizedException()
    {
        // Member userId=2 tries to delete a task created by userId=1
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(2, 10, "other@test.com", "Member"));
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Low, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        Func<Task> act = () => _handler.Handle(new DeleteTaskCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _taskRepo.Verify(r => r.SoftDeleteAsync(It.IsAny<TaskItem>(), It.IsAny<int>()), Times.Never);
    }

    [Test]
    public async Task Handle_TaskNotFound_ThrowsNotFoundException()
    {
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(1, 10, "u@test.com", "Member"));
        _taskRepo.Setup(r => r.GetByIdAsync(999, 10)).ReturnsAsync((TaskItem?)null);

        Func<Task> act = () => _handler.Handle(new DeleteTaskCommand(999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task Handle_NoAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUser.Setup(u => u.User).Returns((CurrentUser?)null);
        var handler = new DeleteTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);

        Func<Task> act = () => handler.Handle(new DeleteTaskCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }
}

[TestFixture]
public class UpdateTaskHandlerTests
{
    private Mock<ITaskRepository> _taskRepo = null!;
    private Mock<ICurrentUserService> _currentUser = null!;
    private Mock<ICacheService> _cache = null!;
    private UpdateTaskHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _taskRepo = new Mock<ITaskRepository>();
        _currentUser = new Mock<ICurrentUserService>();
        _cache = new Mock<ICacheService>();

        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _cache.Setup(c => c.RemoveAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>())).Returns(Task.CompletedTask);

        _handler = new UpdateTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);
    }

    [Test]
    public async Task Handle_OwnerUpdatesTask_UpdatesAndInvalidatesCache()
    {
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(1, 10, "u@test.com", "Member"));
        var task = TaskItem.Create(10, 1, "Old Title", null, TaskPriority.Low, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        var cmd = new UpdateTaskCommand(1, "New Title", null, "High", null, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Title.Should().Be("New Title");
        result.Priority.Should().Be("High");
        _taskRepo.Verify(r => r.UpdateAsync(task), Times.Once);
        _cache.Verify(c => c.RemoveByPatternAsync("tasks:10:*"), Times.Once);
        _cache.Verify(c => c.RemoveAsync("task:10:1"), Times.Once);
    }

    [Test]
    public async Task Handle_NonOwnerMember_ThrowsUnauthorizedException()
    {
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(2, 10, "other@test.com", "Member"));
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Low, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        var cmd = new UpdateTaskCommand(1, "Hacked", null, "Low", null, null);
        Func<Task> act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Test]
    public async Task Handle_AdminUpdatesOthersTask_Succeeds()
    {
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(99, 10, "admin@test.com", "Admin"));
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Low, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        var cmd = new UpdateTaskCommand(1, "Admin Updated", null, "High", null, null);
        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.Title.Should().Be("Admin Updated");
        _taskRepo.Verify(r => r.UpdateAsync(task), Times.Once);
    }

    [Test]
    public async Task Handle_TaskNotFound_ThrowsNotFoundException()
    {
        _currentUser.Setup(u => u.User).Returns(new CurrentUser(1, 10, "u@test.com", "Member"));
        _taskRepo.Setup(r => r.GetByIdAsync(99, 10)).ReturnsAsync((TaskItem?)null);

        var cmd = new UpdateTaskCommand(99, "Title", null, "Low", null, null);
        Func<Task> act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

[TestFixture]
public class StartTaskHandlerTests
{
    private Mock<ITaskRepository> _taskRepo = null!;
    private Mock<ICurrentUserService> _currentUser = null!;
    private Mock<ICacheService> _cache = null!;
    private StartTaskHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _taskRepo = new Mock<ITaskRepository>();
        _currentUser = new Mock<ICurrentUserService>();
        _cache = new Mock<ICacheService>();

        _currentUser.Setup(u => u.User).Returns(new CurrentUser(1, 10, "u@test.com", "Member"));
        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>())).Returns(Task.CompletedTask);

        _handler = new StartTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);
    }

    [Test]
    public async Task Handle_TodoTask_TransitionsToInProgress()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        var result = await _handler.Handle(new StartTaskCommand(1), CancellationToken.None);

        result.Status.Should().Be("InProgress");
        _taskRepo.Verify(r => r.UpdateAsync(task), Times.Once);
    }

    [Test]
    public async Task Handle_AlreadyInProgress_ThrowsDomainException()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        task.Start();
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        Func<Task> act = () => _handler.Handle(new StartTaskCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>();
        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Test]
    public async Task Handle_TaskNotFound_ThrowsNotFoundException()
    {
        _taskRepo.Setup(r => r.GetByIdAsync(999, 10)).ReturnsAsync((TaskItem?)null);

        Func<Task> act = () => _handler.Handle(new StartTaskCommand(999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}

[TestFixture]
public class CancelTaskHandlerTests
{
    private Mock<ITaskRepository> _taskRepo = null!;
    private Mock<ICurrentUserService> _currentUser = null!;
    private Mock<ICacheService> _cache = null!;
    private CancelTaskHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _taskRepo = new Mock<ITaskRepository>();
        _currentUser = new Mock<ICurrentUserService>();
        _cache = new Mock<ICacheService>();

        _currentUser.Setup(u => u.User).Returns(new CurrentUser(1, 10, "u@test.com", "Member"));
        _cache.Setup(c => c.RemoveByPatternAsync(It.IsAny<string>())).Returns(Task.CompletedTask);
        _taskRepo.Setup(r => r.UpdateAsync(It.IsAny<TaskItem>())).Returns(Task.CompletedTask);

        _handler = new CancelTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);
    }

    [Test]
    public async Task Handle_ActiveTask_TransitionsToCancelled()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        var result = await _handler.Handle(new CancelTaskCommand(1), CancellationToken.None);

        result.Status.Should().Be("Cancelled");
        _taskRepo.Verify(r => r.UpdateAsync(task), Times.Once);
    }

    [Test]
    public async Task Handle_CompletedTask_ThrowsDomainException()
    {
        var task = TaskItem.Create(10, 1, "Task", null, TaskPriority.Medium, null);
        task.Complete();
        _taskRepo.Setup(r => r.GetByIdAsync(1, 10)).ReturnsAsync(task);

        Func<Task> act = () => _handler.Handle(new CancelTaskCommand(1), CancellationToken.None);

        await act.Should().ThrowAsync<DomainException>().WithMessage("*completed*");
        _taskRepo.Verify(r => r.UpdateAsync(It.IsAny<TaskItem>()), Times.Never);
    }

    [Test]
    public async Task Handle_TaskNotFound_ThrowsNotFoundException()
    {
        _taskRepo.Setup(r => r.GetByIdAsync(999, 10)).ReturnsAsync((TaskItem?)null);

        Func<Task> act = () => _handler.Handle(new CancelTaskCommand(999), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}