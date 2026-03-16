using FluentAssertions;
using NUnit.Framework;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Events;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Application.Tests.Tasks;

[TestFixture]
public class TaskItemDomainTests
{
    private static TaskItem CreateTask(
        string title = "Test Task",
        TaskPriority priority = TaskPriority.Medium,
        DateTime? dueDate = null,
        string[]? tags = null) =>
        TaskItem.Create(1, 1, title, null, priority, dueDate, tags);

    // ── Create ────────────────────────────────────────────────────────────────

    [Test]
    public void Create_ValidInput_ReturnsTaskWithCorrectDefaults()
    {
        var task = CreateTask("My Task");

        task.Title.Should().Be("My Task");
        task.Status.Should().Be(Domain.Entities.TaskStatus.Todo);
        task.Priority.Should().Be(TaskPriority.Medium);
        task.CompletedAt.Should().BeNull();
        task.AssignedToUserId.Should().BeNull();
        task.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public void Create_WithAllFields_MapsCorrectly()
    {
        var due = DateTime.UtcNow.AddDays(5);
        var task = TaskItem.Create(2, 3, "Full Task", "A description",
            TaskPriority.Critical, due, new[] { "backend", "api" });

        task.TenantId.Should().Be(2);
        task.CreatedByUserId.Should().Be(3);
        task.Description.Should().Be("A description");
        task.Priority.Should().Be(TaskPriority.Critical);
        task.DueDate.Should().Be(due);
        task.Tags.Should().BeEquivalentTo("backend", "api");
    }

    [Test]
    public void Create_EmptyTitle_ThrowsDomainException()
    {
        Action act = () => TaskItem.Create(1, 1, "", null, TaskPriority.Low, null);
        act.Should().Throw<DomainException>().WithMessage("*Title*");
    }

    [Test]
    public void Create_WhiteSpaceTitle_ThrowsDomainException()
    {
        Action act = () => TaskItem.Create(1, 1, "   ", null, TaskPriority.Low, null);
        act.Should().Throw<DomainException>().WithMessage("*Title*");
    }

    [Test]
    public void Create_PastDueDate_ThrowsDomainException()
    {
        Action act = () => TaskItem.Create(1, 1, "Task", null, TaskPriority.Low,
            DateTime.UtcNow.AddDays(-1));
        act.Should().Throw<DomainException>().WithMessage("*past*");
    }

    [Test]
    public void Create_TodayDueDate_DoesNotThrow()
    {
        Action act = () => TaskItem.Create(1, 1, "Task", null, TaskPriority.Low,
            DateTime.UtcNow.Date);
        act.Should().NotThrow();
    }

    [Test]
    public void Create_NullTags_DefaultsToEmptyArray()
    {
        var task = TaskItem.Create(1, 1, "Task", null, TaskPriority.Low, null, null);
        task.Tags.Should().BeEmpty();
    }

    // ── Start ─────────────────────────────────────────────────────────────────

    [Test]
    public void Start_FromTodo_TransitionsToInProgress()
    {
        var task = CreateTask();
        task.Start();
        task.Status.Should().Be(Domain.Entities.TaskStatus.InProgress);
        task.UpdatedAt.Should().NotBeNull();
    }

    [Test]
    public void Start_AlreadyInProgress_ThrowsDomainException()
    {
        var task = CreateTask();
        task.Start();
        Action act = () => task.Start();
        act.Should().Throw<DomainException>().WithMessage("*Todo*");
    }

    [Test]
    public void Start_FromDone_ThrowsDomainException()
    {
        var task = CreateTask();
        task.Complete();
        Action act = () => task.Start();
        act.Should().Throw<DomainException>();
    }

    [Test]
    public void Start_FromCancelled_ThrowsDomainException()
    {
        var task = CreateTask();
        task.Cancel();
        Action act = () => task.Start();
        act.Should().Throw<DomainException>();
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    [Test]
    public void Complete_FromTodo_TransitionsToDoneAndSetsCompletedAt()
    {
        var task = CreateTask();
        var before = DateTime.UtcNow;
        task.Complete();

        task.Status.Should().Be(Domain.Entities.TaskStatus.Done);
        task.CompletedAt.Should().NotBeNull();
        task.CompletedAt!.Value.Should().BeOnOrAfter(before);
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
    public void Complete_AlreadyDone_IsIdempotent()
    {
        var task = CreateTask();
        task.Complete();
        task.ClearDomainEvents();

        Action act = () => task.Complete();
        act.Should().NotThrow();
        task.DomainEvents.Should().BeEmpty();
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
    public void Complete_RaisesEventWithCorrectData()
    {
        var task = TaskItem.Create(5, 7, "Important Task", null, TaskPriority.High, null);
        task.Complete();

        var evt = task.DomainEvents.OfType<TaskCompletedEvent>().Single();
        evt.TenantId.Should().Be(5);
        evt.UserId.Should().Be(7);
        evt.Title.Should().Be("Important Task");
        evt.CompletedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    // ── Cancel ────────────────────────────────────────────────────────────────

    [Test]
    public void Cancel_FromTodo_TransitionsToCancelled()
    {
        var task = CreateTask();
        task.Cancel();
        task.Status.Should().Be(Domain.Entities.TaskStatus.Cancelled);
        task.UpdatedAt.Should().NotBeNull();
    }

    [Test]
    public void Cancel_FromInProgress_TransitionsToCancelled()
    {
        var task = CreateTask();
        task.Start();
        task.Cancel();
        task.Status.Should().Be(Domain.Entities.TaskStatus.Cancelled);
    }

    [Test]
    public void Cancel_ValidTask_RaisesCancelledEvent()
    {
        var task = CreateTask();
        task.Cancel();
        task.DomainEvents.Should().ContainSingle(e => e is TaskCancelledEvent);
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
    public void Cancel_RaisesEventWithCorrectData()
    {
        var task = TaskItem.Create(3, 1, "My Task", null, TaskPriority.Low, null);
        task.Cancel();

        var evt = task.DomainEvents.OfType<TaskCancelledEvent>().Single();
        evt.TenantId.Should().Be(3);
        evt.Title.Should().Be("My Task");
    }

    // ── Assign ────────────────────────────────────────────────────────────────

    [Test]
    public void Assign_ValidUser_SetsAssignedToUserIdAndRaisesEvent()
    {
        var task = CreateTask();
        task.Assign(42);

        task.AssignedToUserId.Should().Be(42);
        task.DomainEvents.Should().ContainSingle(e => e is TaskAssignedEvent);
    }

    [Test]
    public void Assign_CanReassignToAnotherUser()
    {
        var task = CreateTask();
        task.Assign(10);
        task.ClearDomainEvents();
        task.Assign(20);

        task.AssignedToUserId.Should().Be(20);
        task.DomainEvents.Should().ContainSingle(e => e is TaskAssignedEvent);
    }

    [Test]
    public void Assign_RaisesEventWithCorrectData()
    {
        var task = TaskItem.Create(4, 1, "Task", null, TaskPriority.Medium, null);
        task.Assign(99);

        var evt = task.DomainEvents.OfType<TaskAssignedEvent>().Single();
        evt.TenantId.Should().Be(4);
        evt.AssignedToUserId.Should().Be(99);
        evt.Title.Should().Be("Task");
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Test]
    public void Update_ValidInput_UpdatesFields()
    {
        var task = CreateTask("Old Title");
        var newDue = DateTime.UtcNow.AddDays(10);

        task.Update("New Title", "New desc", TaskPriority.Critical, newDue, new[] { "tag1" });

        task.Title.Should().Be("New Title");
        task.Description.Should().Be("New desc");
        task.Priority.Should().Be(TaskPriority.Critical);
        task.DueDate.Should().Be(newDue);
        task.Tags.Should().Contain("tag1");
        task.UpdatedAt.Should().NotBeNull();
    }

    [Test]
    public void Update_EmptyTitle_ThrowsDomainException()
    {
        var task = CreateTask();
        Action act = () => task.Update("", null, TaskPriority.Low, null, null);
        act.Should().Throw<DomainException>().WithMessage("*Title*");
    }

    [Test]
    public void Update_PastDueDate_ThrowsDomainException()
    {
        var task = CreateTask();
        Action act = () => task.Update("Title", null, TaskPriority.Low,
            DateTime.UtcNow.AddDays(-1), null);
        act.Should().Throw<DomainException>().WithMessage("*past*");
    }

    [Test]
    public void Update_NullTags_DefaultsToEmptyArray()
    {
        var task = CreateTask("Task", tags: new[] { "old" });
        task.Update("Task", null, TaskPriority.Low, null, null);
        task.Tags.Should().BeEmpty();
    }

    // ── SoftDelete ────────────────────────────────────────────────────────────

    [Test]
    public void SoftDelete_SetsIsDeletedAndTimestamp()
    {
        var task = CreateTask();
        var before = DateTime.UtcNow;
        task.SoftDelete(99);

        task.IsDeleted.Should().BeTrue();
        task.DeletedAt.Should().NotBeNull();
        task.DeletedAt!.Value.Should().BeOnOrAfter(before);
        task.DeletedByUserId.Should().Be(99);
    }

    // ── Domain Events ─────────────────────────────────────────────────────────

    [Test]
    public void ClearDomainEvents_RemovesAllEvents()
    {
        var task = CreateTask();
        task.Complete();
        task.DomainEvents.Should().NotBeEmpty();

        task.ClearDomainEvents();
        task.DomainEvents.Should().BeEmpty();
    }

    [Test]
    public void MultipleOperations_AccumulateEvents()
    {
        var task = CreateTask();
        task.Assign(5);
        task.Complete();

        task.DomainEvents.Should().HaveCount(2);
        task.DomainEvents.Should().Contain(e => e is TaskAssignedEvent);
        task.DomainEvents.Should().Contain(e => e is TaskCompletedEvent);
    }

    [Test]
    public void AllEvents_HaveOccurredAtSet()
    {
        var task = CreateTask();
        task.Complete();

        foreach (var evt in task.DomainEvents)
            evt.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }
}