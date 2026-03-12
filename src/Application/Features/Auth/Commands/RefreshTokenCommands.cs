using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Features.Auth.Commands;

// ── Refresh Token ─────────────────────────────────────────────────────────────

public record RefreshTokenCommand(string AccessToken, string RefreshToken) : IRequest<AuthResult>;

public class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, AuthResult>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUserRepository _users;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;

    public RefreshTokenHandler(IRefreshTokenRepository refreshTokens, IUserRepository users,
        IJwtService jwt, IAuditService audit)
    {
        _refreshTokens = refreshTokens;
        _users = users;
        _jwt = jwt;
        _audit = audit;
    }

    public async Task<AuthResult> Handle(RefreshTokenCommand cmd, CancellationToken ct)
    {
        var existingToken = await _refreshTokens.GetByTokenAsync(cmd.RefreshToken)
            ?? throw new UnauthorizedException("Invalid refresh token.");

        if (!existingToken.IsActive)
            throw new UnauthorizedException("Refresh token is expired or revoked.");

        var user = await _users.GetByIdAsync(existingToken.UserId, existingToken.TenantId)
            ?? throw new UnauthorizedException("User not found.");

        if (!user.IsActive) throw new UnauthorizedException("Account is inactive.");

        // Rotate refresh token
        var newRefreshToken = _jwt.GenerateRefreshToken();
        existingToken.Revoke(newRefreshToken);
        await _refreshTokens.UpdateAsync(existingToken);

        var newToken = RefreshToken.Create(user.Id, user.TenantId, newRefreshToken);
        await _refreshTokens.AddAsync(newToken);

        var accessToken = _jwt.GenerateAccessToken(user.Id, user.TenantId, user.Email, user.Role.ToString());

        await _audit.LogAsync("Auth.TokenRefreshed", "User", user.Id);

        return new AuthResult(accessToken, newRefreshToken, user.Email, user.FullName, user.Role.ToString(), user.TenantId);
    }
}

// ── Revoke Token ──────────────────────────────────────────────────────────────

public record RevokeTokenCommand(string RefreshToken) : IRequest<Unit>;

public class RevokeTokenHandler : IRequestHandler<RevokeTokenCommand, Unit>
{
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IAuditService _audit;

    public RevokeTokenHandler(IRefreshTokenRepository refreshTokens, IAuditService audit)
    {
        _refreshTokens = refreshTokens;
        _audit = audit;
    }

    public async Task<Unit> Handle(RevokeTokenCommand cmd, CancellationToken ct)
    {
        var token = await _refreshTokens.GetByTokenAsync(cmd.RefreshToken)
            ?? throw new NotFoundException("RefreshToken", cmd.RefreshToken);

        token.Revoke();
        await _refreshTokens.UpdateAsync(token);
        await _audit.LogAsync("Auth.TokenRevoked", "RefreshToken", token.Id);

        return Unit.Value;
    }
}
