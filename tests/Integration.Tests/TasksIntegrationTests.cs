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

    [Test, Order(1)]
    public async Task CreateTask_ValidData_Returns201()
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
    }

    [Test, Order(2)]
    public async Task GetTasks_Authenticated_ReturnsPaginatedList()
    {
        var response = await Client.GetAsync("/api/tasks?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().NotBeEmpty();
        result.Page.Should().Be(1);
        result.PageSize.Should().Be(10);
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
        var response = await Client.GetAsync("/api/tasks?priority=High");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().OnlyContain(t => t.Priority == "High");
    }

    [Test, Order(5)]
    public async Task GetTasks_SearchByTitle_ReturnsMatchingTasks()
    {
        var response = await Client.GetAsync("/api/tasks?search=Integration");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PagedResult<TaskDto>>();
        result!.Items.Should().NotBeEmpty();
    }

    [Test, Order(6)]
    public async Task StartTask_ValidTransition_ReturnsInProgress()
    {
        // Create a task first
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

    [Test, Order(7)]
    public async Task CompleteTask_ValidTransition_ReturnsDone()
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

    [Test, Order(8)]
    public async Task DeleteTask_ExistingTask_Returns204()
    {
        var create = await Client.PostAsJsonAsync("/api/tasks", new
        {
            title = "Task to Delete",
            priority = "Low"
        });
        var task = await create.Content.ReadFromJsonAsync<TaskDto>();

        var response = await Client.DeleteAsync($"/api/tasks/{task!.Id}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Verify soft deleted — should return 404
        var getResponse = await Client.GetAsync($"/api/tasks/{task.Id}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test, Order(9)]
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

    [Test, Order(10)]
    public async Task GetTaskById_WithComment_ReturnsComments()
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
    }

    [Test]
    public async Task GetTasks_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.GetAsync("/api/tasks");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        AuthorizeClient((await LoginAsync(_slug, "tasks@test.com", "Password123!")).AccessToken);
    }
}
