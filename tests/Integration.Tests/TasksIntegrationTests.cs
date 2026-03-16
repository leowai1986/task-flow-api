using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Application.Common.Models;

namespace TaskFlow.Integration.Tests;

[TestFixture]
public class TasksIntegrationTests : BaseIntegrationTest
{
    private string _slug = null!;

    [OneTimeSetUp]
    public new async Task OneTimeSetUp()
    {
        base.OneTimeSetUp();
        _slug = "tasks-test-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(_slug, "tasks@test.com", "Password123!");
        AuthorizeClient(auth.AccessToken);
    }

    // ── Create ────────────────────────────────────────────────────────────────

    [Test, Order(1)]
    public async Task CreateTask_ValidData_Returns201WithCorrectFields()
    {
        var response = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Integration Test Task",
            description = "Created from integration test",
            priority = "High",
            dueDate = DateTime.UtcNow.AddDays(7),
            tags = new[] { "test", "integration" }
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>();
        task!.Title.Should().Be("Integration Test Task");
        task.Status.Should().Be("Todo");
        task.Priority.Should().Be("High");
        task.Tags.Should().Contain("integration");
        task.Tags.Should().Contain("test");
        task.Id.Should().BeGreaterThan(0);
        task.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Test]
    public async Task CreateTask_EmptyTitle_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "",
            priority = "High"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateTask_InvalidPriority_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Valid title",
            priority = "SuperUrgent"
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateTask_PastDueDate_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task",
            priority = "Low",
            dueDate = DateTime.UtcNow.AddDays(-1)
        });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CreateTask_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task",
            priority = "Low"
        });
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AuthorizeClient((await LoginAsync(_slug, "tasks@test.com", "Password123!")).AccessToken);
    }

    // ── Get / Filter ──────────────────────────────────────────────────────────

    [Test, Order(2)]
    public async Task GetTasks_Authenticated_ReturnsPaginatedList()
    {
        var response = await Client.GetAsync("/api/tasks?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().NotBeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.TotalCount.Should().BeGreaterThan(0);
    }

    [Test, Order(3)]
    public async Task GetTasks_FilterByStatus_ReturnsOnlyMatchingTasks()
    {
        var response = await Client.GetAsync("/api/tasks?status=Todo");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().OnlyContain(t => t.Status == "Todo");
    }

    [Test, Order(4)]
    public async Task GetTasks_FilterByPriority_ReturnsOnlyMatchingTasks()
    {
        // Create a known High priority task first
        await Client.PostAsJsonAsync("/api/tasks", new { title = "High Prio Task", priority = "High" });

        var response = await Client.GetAsync("/api/tasks?priority=High");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().OnlyContain(t => t.Priority == "High");
    }

    [Test, Order(5)]
    public async Task GetTasks_SearchByTitle_ReturnsMatchingTasks()
    {
        var uniqueTitle = "UniqueSearchTitle_" + Guid.NewGuid().ToString("N")[..6];
        await Client.PostAsJsonAsync("/api/tasks", new { title = uniqueTitle, priority = "Low" });

        var response = await Client.GetAsync($"/api/tasks?search={uniqueTitle}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().NotBeEmpty();
        result.Items.Should().OnlyContain(t => t.Title.Contains(uniqueTitle));
    }

    [Test]
    public async Task GetTasks_PageSizeCappedAt100()
    {
        var response = await Client.GetAsync("/api/tasks?pageSize=999");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.PageSize.Should().BeLessOrEqualTo(100);
    }

    [Test]
    public async Task GetTasks_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/tasks");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AuthorizeClient((await LoginAsync(_slug, "tasks@test.com", "Password123!")).AccessToken);
    }

    // ── Get By Id ─────────────────────────────────────────────────────────────

    [Test, Order(6)]
    public async Task GetTaskById_ExistingTask_ReturnsFullDetails()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Detail Task",
            description = "Full details",
            priority = "Medium"
        });
        var created = await create.Content.ReadFromJsonAsync<TaskDto>();

        var response = await Client.GetAsync($"/api/tasks/{created!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var task = await response.Content.ReadFromJsonAsync<TaskDto>();
        task!.Id.Should().Be(created.Id);
        task.Title.Should().Be("Detail Task");
        task.Description.Should().Be("Full details");
    }

    [Test]
    public async Task GetTaskById_NotFound_Returns404()
    {
        var response = await Client.GetAsync("/api/tasks/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── State Transitions ─────────────────────────────────────────────────────

    [Test, Order(7)]
    public async Task StartTask_ValidTransition_ReturnsInProgress()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Start",
            priority = "Medium"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();

        var response = await Client.PatchAsync($"/api/tasks/{task!.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TaskDto>();
        updated!.Status.Should().Be("InProgress");
    }

    [Test]
    public async Task StartTask_AlreadyInProgress_Returns400()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Double Start",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();
        await Client.PatchAsync($"/api/tasks/{task!.Id}/start", null);

        // Second start — invalid transition
        var response = await Client.PatchAsync($"/api/tasks/{task.Id}/start", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test, Order(8)]
    public async Task CompleteTask_ValidTransition_ReturnsDoneWithCompletedAt()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Complete",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();
        await Client.PatchAsync($"/api/tasks/{task!.Id}/start", null);

        var response = await Client.PatchAsync($"/api/tasks/{task.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TaskDto>();
        updated!.Status.Should().Be("Done");
        updated.CompletedAt.Should().NotBeNull();
    }

    [Test]
    public async Task CompleteTask_AlreadyCancelled_Returns400()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Cancel then Complete",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();
        await Client.PatchAsync($"/api/tasks/{task!.Id}/cancel", null);

        var response = await Client.PatchAsync($"/api/tasks/{task.Id}/complete", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task CancelTask_ValidTransition_ReturnsCancelled()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Cancel",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();

        var response = await Client.PatchAsync($"/api/tasks/{task!.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<TaskDto>();
        updated!.Status.Should().Be("Cancelled");
    }

    [Test]
    public async Task CancelTask_AlreadyDone_Returns400()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Complete then Cancel",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();
        await Client.PatchAsync($"/api/tasks/{task!.Id}/complete", null);

        var response = await Client.PatchAsync($"/api/tasks/{task.Id}/cancel", null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    [Test, Order(9)]
    public async Task DeleteTask_ExistingTask_Returns204AndSoftDeletes()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Delete",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();

        var response = await Client.DeleteAsync($"/api/tasks/{task!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Soft deleted task should be invisible — returns 404
        var getResponse = await Client.GetAsync($"/api/tasks/{task.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task DeleteTask_NotFound_Returns404()
    {
        var response = await Client.DeleteAsync("/api/tasks/99999");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Multi-tenant isolation ────────────────────────────────────────────────

    [Test]
    public async Task GetTaskById_TaskFromAnotherTenant_Returns404()
    {
        // Tenant A creates a task
        var slugA = "tenant-a-" + Guid.NewGuid().ToString("N")[..6];
        var authA = await RegisterTenantAsync(slugA, "a@tenanttest.com", "Password123!");
        AuthorizeClient(authA.AccessToken);

        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Tenant A's private task",
            priority = "High"
        });
        var taskA = await create.Content.ReadFromJsonAsync<TaskDto>();

        // Tenant B tries to access Tenant A's task
        var slugB = "tenant-b-" + Guid.NewGuid().ToString("N")[..6];
        var authB = await RegisterTenantAsync(slugB, "b@tenanttest.com", "Password123!");
        AuthorizeClient(authB.AccessToken);

        var response = await Client.GetAsync($"/api/tasks/{taskA!.Id}");

        // Tenant B cannot see Tenant A's data — 404 not 403
        // (leaking the existence of the resource is also a security issue)
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Restore original auth
        AuthorizeClient((await LoginAsync(_slug, "tasks@test.com", "Password123!")).AccessToken);
    }

    [Test]
    public async Task DeleteTask_TaskFromAnotherTenant_Returns404()
    {
        // Tenant A creates a task
        var slugA = "del-a-" + Guid.NewGuid().ToString("N")[..6];
        var authA = await RegisterTenantAsync(slugA, "dela@test.com", "Password123!");
        AuthorizeClient(authA.AccessToken);

        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Tenant A task",
            priority = "Low"
        });
        var taskA = await create.Content.ReadFromJsonAsync<TaskDto>();

        // Tenant B tries to delete it
        var slugB = "del-b-" + Guid.NewGuid().ToString("N")[..6];
        var authB = await RegisterTenantAsync(slugB, "delb@test.com", "Password123!");
        AuthorizeClient(authB.AccessToken);

        var response = await Client.DeleteAsync($"/api/tasks/{taskA!.Id}");
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Restore
        AuthorizeClient((await LoginAsync(_slug, "tasks@test.com", "Password123!")).AccessToken);
    }

    // ── Comments ──────────────────────────────────────────────────────────────

    [Test, Order(10)]
    public async Task AddComment_ValidTask_Returns201()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task with Comment",
            priority = "Medium"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();

        var response = await Client.PostAsJsonAsync($"/api/tasks/{task!.Id}/comments", new
        {
            content = "This is a test comment"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Test, Order(11)]
    public async Task GetTaskById_WithComments_ReturnsCommentCount()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task with Comments Check",
            priority = "Medium"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();
        await Client.PostAsJsonAsync($"/api/tasks/{task!.Id}/comments", new { content = "Comment 1" });
        await Client.PostAsJsonAsync($"/api/tasks/{task.Id}/comments", new { content = "Comment 2" });

        var response = await Client.GetAsync($"/api/tasks/{task.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<TaskDto>();
        result!.CommentCount.Should().Be(2);
        result.Comments.Should().HaveCount(2);
    }

    [Test]
    public async Task AddComment_TaskNotFound_Returns404()
    {
        var response = await Client.PostAsJsonAsync("/api/tasks/99999/comments", new
        {
            content = "Comment on non-existent task"
        });
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}