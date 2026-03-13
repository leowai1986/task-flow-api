using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Application.Features.Metrics.Queries;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class MetricsController : ControllerBase
{
    private readonly IMediator _mediator;
    public MetricsController(IMediator mediator) => _mediator = mediator;

    /// <summary>
    /// Get task metrics for the current tenant — totals by status, overdue count,
    /// average completion time, top tags and tasks per user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(TenantMetricsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetrics() =>
        Ok(await _mediator.Send(new GetTenantMetricsQuery()));
}
