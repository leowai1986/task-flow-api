using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Common.Models;
using TaskFlow.Application.Features.Comments.Commands;
using TaskFlow.Application.Features.Tasks.Commands;
using TaskFlow.Application.Features.Tasks.Queries;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TasksController : ControllerBase
{
    private readonly IMediator _mediator;
    public TasksController(IMediator mediator) => _mediator = mediator;

    /// <summary>Get tasks with filtering, sorting and pagination</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<TaskDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] string? priority,
        [FromQuery] int? assignedToUserId,
        [FromQuery] DateTime? dueBefore,
        [FromQuery] string? tag,
        [FromQuery] string sortBy = "CreatedAt",
        [FromQuery] bool sortDescending = true,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var result = await _mediator.Send(new GetTasksQuery(
            search, status, priority, assignedToUserId,
            dueBefore, tag, sortBy, sortDescending, page, pageSize));
        return Ok(result);
    }

    /// <summary>Get a task by ID with full details and comments</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id) =>
        Ok(await _mediator.Send(new GetTaskByIdQuery(id)));

    /// <summary>Create a new task</summary>
    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateTaskCommand cmd)
    {
        var result = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    /// <summary>Update a task (title, description, priority, due date, tags)</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateTaskCommand cmd) =>
        Ok(await _mediator.Send(cmd with { Id = id }));

    /// <summary>Start a task (Todo → InProgress)</summary>
    [HttpPatch("{id:int}/start")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(int id) =>
        Ok(await _mediator.Send(new StartTaskCommand(id)));

    /// <summary>Complete a task (any → Done). Fires TaskCompletedEvent.</summary>
    [HttpPatch("{id:int}/complete")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(int id) =>
        Ok(await _mediator.Send(new CompleteTaskCommand(id)));

    /// <summary>Cancel a task. Fires TaskCancelledEvent.</summary>
    [HttpPatch("{id:int}/cancel")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(int id) =>
        Ok(await _mediator.Send(new CancelTaskCommand(id)));

    /// <summary>Assign a task to a user. Fires TaskAssignedEvent.</summary>
    [HttpPatch("{id:int}/assign")]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(int id, [FromBody] AssignTaskCommand cmd) =>
        Ok(await _mediator.Send(cmd with { Id = id }));

    /// <summary>Delete a task (owner or Admin only)</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(int id)
    {
        await _mediator.Send(new DeleteTaskCommand(id));
        return NoContent();
    }

    /// <summary>Add a comment to a task</summary>
    [HttpPost("{id:int}/comments")]
    [ProducesResponseType(typeof(CommentDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> AddComment(int id, [FromBody] AddCommentCommand cmd)
    {
        var result = await _mediator.Send(cmd with { TaskId = id });
        return StatusCode(201, result);
    }
}
