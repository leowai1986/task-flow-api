using FluentValidation;
using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Exceptions;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Application.Features.Auth.Commands;

// ── Models ────────────────────────────────────────────────────────────────────

public record AuthResult(
    string AccessToken, string RefreshToken,
    string Email, string FullName, string Role, int TenantId
);

// ── Register ──────────────────────────────────────────────────────────────────

public record RegisterCommand(string TenantSlug, string Email, string FullName, string Password) : IRequest<AuthResult>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.TenantSlug).NotEmpty();
    }
}

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResult>
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;

    public RegisterCommandHandler(IUserRepository users, ITenantRepository tenants,
        IRefreshTokenRepository refreshTokens, IPasswordHasher hasher,
        IJwtService jwt, IAuditService audit)
    {
        _users = users; _tenants = tenants; _refreshTokens = refreshTokens;
        _hasher = hasher; _jwt = jwt; _audit = audit;
    }

    public async Task<AuthResult> Handle(RegisterCommand cmd, CancellationToken ct)
    {
        var tenant = await _tenants.GetBySlugAsync(cmd.TenantSlug)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        if (!tenant.IsActive) throw new DomainException("Tenant is inactive.");
        if (await _users.ExistsAsync(cmd.Email, tenant.Id))
            throw new DomainException("Email is already registered for this tenant.");

        var user = User.Create(tenant.Id, cmd.Email, cmd.FullName, _hasher.Hash(cmd.Password));
        await _users.AddAsync(user);

        var (accessToken, refreshToken) = await GenerateTokensAsync(user);
        await _audit.LogAsync("Auth.Registered", "User", user.Id);

        return new AuthResult(accessToken, refreshToken, user.Email, user.FullName, user.Role.ToString(), tenant.Id);
    }

    private async Task<(string access, string refresh)> GenerateTokensAsync(User user)
    {
        var access = _jwt.GenerateAccessToken(user.Id, user.TenantId, user.Email, user.Role.ToString());
        var refreshRaw = _jwt.GenerateRefreshToken();
        await _refreshTokens.AddAsync(RefreshToken.Create(user.Id, user.TenantId, refreshRaw));
        return (access, refreshRaw);
    }
}

// ── Login ─────────────────────────────────────────────────────────────────────

public record LoginCommand(string TenantSlug, string Email, string Password) : IRequest<AuthResult>;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
        RuleFor(x => x.TenantSlug).NotEmpty();
    }
}

public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResult>
{
    private readonly IUserRepository _users;
    private readonly ITenantRepository _tenants;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;

    public LoginCommandHandler(IUserRepository users, ITenantRepository tenants,
        IRefreshTokenRepository refreshTokens, IPasswordHasher hasher,
        IJwtService jwt, IAuditService audit)
    {
        _users = users; _tenants = tenants; _refreshTokens = refreshTokens;
        _hasher = hasher; _jwt = jwt; _audit = audit;
    }

    public async Task<AuthResult> Handle(LoginCommand cmd, CancellationToken ct)
    {
        var tenant = await _tenants.GetBySlugAsync(cmd.TenantSlug)
            ?? throw new NotFoundException("Tenant", cmd.TenantSlug);

        var user = await _users.GetByEmailAsync(cmd.Email, tenant.Id)
            ?? throw new UnauthorizedException("Invalid credentials.");

        if (!user.IsActive) throw new UnauthorizedException("Account is inactive.");
        if (!_hasher.Verify(cmd.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials.");

        var access = _jwt.GenerateAccessToken(user.Id, user.TenantId, user.Email, user.Role.ToString());
        var refreshRaw = _jwt.GenerateRefreshToken();
        await _refreshTokens.AddAsync(RefreshToken.Create(user.Id, user.TenantId, refreshRaw));
        await _audit.LogAsync("Auth.LoggedIn", "User", user.Id);

        return new AuthResult(access, refreshRaw, user.Email, user.FullName, user.Role.ToString(), tenant.Id);
    }
}

// ── Create Tenant ─────────────────────────────────────────────────────────────

public record CreateTenantCommand(string Name, string Slug, string AdminEmail,
    string AdminFullName, string AdminPassword) : IRequest<AuthResult>;

public class CreateTenantCommandHandler : IRequestHandler<CreateTenantCommand, AuthResult>
{
    private readonly ITenantRepository _tenants;
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtService _jwt;
    private readonly IAuditService _audit;

    public CreateTenantCommandHandler(ITenantRepository tenants, IUserRepository users,
        IRefreshTokenRepository refreshTokens, IPasswordHasher hasher,
        IJwtService jwt, IAuditService audit)
    {
        _tenants = tenants; _users = users; _refreshTokens = refreshTokens;
        _hasher = hasher; _jwt = jwt; _audit = audit;
    }

    public async Task<AuthResult> Handle(CreateTenantCommand cmd, CancellationToken ct)
    {
        if (await _tenants.SlugExistsAsync(cmd.Slug))
            throw new DomainException($"Slug '{cmd.Slug}' is already taken.");

        var tenant = Tenant.Create(cmd.Name, cmd.Slug);
        await _tenants.AddAsync(tenant);

        var admin = User.Create(tenant.Id, cmd.AdminEmail, cmd.AdminFullName,
            _hasher.Hash(cmd.AdminPassword), UserRole.Admin);
        await _users.AddAsync(admin);

        var access = _jwt.GenerateAccessToken(admin.Id, tenant.Id, admin.Email, admin.Role.ToString());
        var refreshRaw = _jwt.GenerateRefreshToken();
        await _refreshTokens.AddAsync(RefreshToken.Create(admin.Id, tenant.Id, refreshRaw));
        await _audit.LogAsync("Tenant.Created", "Tenant", tenant.Id);

        return new AuthResult(access, refreshRaw, admin.Email, admin.FullName, admin.Role.ToString(), tenant.Id);
    }
}
