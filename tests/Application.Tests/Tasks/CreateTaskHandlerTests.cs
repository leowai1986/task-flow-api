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
        _taskRepo.Verify(r => r.AddAsync(It.IsAny<TaskItem>()), Times.Once);
        _cache.Verify(c => c.RemoveByPatternAsync($"tasks:10:*"), Times.Once);
    }

    [Test]
    public async Task Handle_NoAuthenticatedUser_ThrowsUnauthorizedException()
    {
        _currentUser.Setup(u => u.User).Returns((CurrentUser?)null);
        var handler = new CreateTaskHandler(_taskRepo.Object, _currentUser.Object, _cache.Object);

        Func<Task> act = () => handler.Handle(
            new CreateTaskCommand("Title", null, "Low", null, null), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedException>();
    }

    [Test]
    public async Task Handle_InvalidPriority_ThrowsDomainException()
    {
        var cmd = new CreateTaskCommand("Title", null, "InvalidPriority", null, null);

        Func<Task> act = () => _handler.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();
    }
}
