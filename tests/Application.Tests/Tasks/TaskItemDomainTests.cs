using FluentAssertions;
using NUnit.Framework;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Application.Tests.Tasks;

[TestFixture]
public class TaskItemDomainTests
{
    private TaskItem CreateTask(string title = "Test Task") =>
        TaskItem.Create(1, 1, title, null, TaskPriority.Medium, null);

    [Test]
    public void Create_ValidInput_ReturnsTaskWithCorrectDefaults()
    {
        var task = CreateTask("My Task");

        task.Title.Should().Be("My Task");
        task.Status.Should().Be(Domain.Entities.TaskStatus.Todo);
        task.Priority.Should().Be(TaskPriority.Medium);
        task.IsCompleted_NotAProperty_UseStatus();
    }

    [Test]
    public void Create_EmptyTitle_ThrowsDomainException()
    {
        Action act = () => TaskItem.Create(1, 1, "", null, TaskPriority.Low, null);
        act.Should().Throw<DomainException>().WithMessage("*Title*");
    }

    [Test]
    public void Create_PastDueDate_ThrowsDomainException()
    {
        Action act = () => TaskItem.Create(1, 1, "Task", null, TaskPriority.Low, DateTime.UtcNow.AddDays(-1));
        act.Should().Throw<DomainException>().WithMessage("*past*");
    }

    [Test]
    public void Start_FromTodo_TransitionsToInProgress()
    {
        var task = CreateTask();
        task.Start();
        task.Status.Should().Be(Domain.Entities.TaskStatus.InProgress);
    }

    [Test]
    public void Start_AlreadyInProgress_ThrowsDomainException()
    {
        var task = CreateTask();
        task.Start();
        Action act = () => task.Start();
        act.Should().Throw<DomainException>();
    }

    [Test]
    public void Complete_FromInProgress_TransitionsToDoneAndRaisesEvent()
    {
        var task = CreateTask();
        task.Start();
        task.Complete();

        task.Status.Should().Be(Domain.Entities.TaskStatus.Done);
        task.CompletedAt.Should().NotBeNull();
        task.DomainEvents.Should().ContainSingle(e => e is TaskCompletedEvent);
    }

    [Test]
    public void Complete_AlreadyCancelled_ThrowsDomainException()
    {
        var task = CreateTask();
        task.Cancel();
        Action act = () => task.Complete();
        act.Should().Throw<DomainException>().WithMessage("*cancelled*");
    }

    [Test]
    public void Cancel_CompletedTask_ThrowsDomainException()
    {
        var task = CreateTask();
        task.Complete();
        Action act = () => task.Cancel();
        act.Should().Throw<DomainException>().WithMessage("*completed*");
    }

    [Test]
    public void Cancel_ValidTask_RaisesCancelledEvent()
    {
        var task = CreateTask();
        task.Cancel();
        task.DomainEvents.Should().ContainSingle(e => e is TaskCancelledEvent);
    }

    [Test]
    public void Assign_ValidUser_RaisesAssignedEvent()
    {
        var task = CreateTask();
        task.Assign(42);
        task.AssignedToUserId.Should().Be(42);
        task.DomainEvents.Should().ContainSingle(e => e is TaskAssignedEvent);
    }

    [Test]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var task = CreateTask();
        task.Complete();
        task.DomainEvents.Should().NotBeEmpty();
        task.ClearDomainEvents();
        task.DomainEvents.Should().BeEmpty();
    }
}

// Extension to keep tests readable without accessing IsCompleted (not on entity)
internal static class TaskTestExtensions
{
    internal static void IsCompleted_NotAProperty_UseStatus(this TaskItem task)
    {
        // Status-based check — IsCompleted is not exposed; Status is the source of truth
        task.Status.Should().NotBe(Domain.Entities.TaskStatus.Done);
    }
}
