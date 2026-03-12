using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TaskFlow.Application.Features.Auth.Commands;

namespace TaskFlow.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    public AuthController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create a new tenant with an admin user</summary>
    [HttpPost("register-tenant")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> RegisterTenant([FromBody] CreateTenantCommand cmd) =>
        StatusCode(201, await _mediator.Send(cmd));

    /// <summary>Register a new user in an existing tenant</summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand cmd) =>
        StatusCode(201, await _mediator.Send(cmd));

    /// <summary>Login — returns access token + refresh token</summary>
    [HttpPost("login")]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Login([FromBody] LoginCommand cmd) =>
        Ok(await _mediator.Send(cmd));

    /// <summary>Refresh an expired access token using a refresh token</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(AuthResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenCommand cmd) =>
        Ok(await _mediator.Send(cmd));

    /// <summary>Revoke a refresh token (logout)</summary>
    [HttpPost("revoke")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenCommand cmd)
    {
        await _mediator.Send(cmd);
        return NoContent();
    }
}
