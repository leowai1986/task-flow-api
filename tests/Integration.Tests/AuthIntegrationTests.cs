using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using TaskFlow.Application.Features.Auth.Commands;

namespace TaskFlow.Integration.Tests;

[TestFixture]
public class AuthIntegrationTests : BaseIntegrationTest
{
    // ── Register Tenant ───────────────────────────────────────────────────────

    [Test]
    public async Task RegisterTenant_ValidData_Returns201WithTokensAndAdminRole()
    {
        var slug = "acme-" + Guid.NewGuid().ToString("N")[..6];

        var response = await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "Acme Corp",
            slug,
            adminEmail = "admin@acme.com",
            adminFullName = "Admin User",
            adminPassword = "SecurePass123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        result.Should().NotBeNull();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Role.Should().Be("Admin");
        result.Email.Should().Be("admin@acme.com");
    }

    [Test]
    public async Task RegisterTenant_DuplicateSlug_Returns400()
    {
        var slug = "duplicate-" + Guid.NewGuid().ToString("N")[..6];

        await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "First",
            slug,
            adminEmail = "a@a.com",
            adminFullName = "A",
            adminPassword = "Password123!"
        });

        var response = await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "Second",
            slug,
            adminEmail = "b@b.com",
            adminFullName = "B",
            adminPassword = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task RegisterTenant_AccessTokenIsValid_CanAccessProtectedEndpoint()
    {
        var slug = "valid-token-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(slug, "user@valid.com", "Password123!");

        AuthorizeClient(auth.AccessToken);
        var response = await Client.GetAsync("/api/tasks");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ClearAuth();
    }

    // ── Register User ─────────────────────────────────────────────────────────

    [Test]
    public async Task Register_ValidUser_Returns201WithMemberRole()
    {
        var slug = "reg-user-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug, "admin@reg.com", "Password123!");

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            tenantSlug = slug,
            email = "member@reg.com",
            fullName = "New Member",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        result!.Role.Should().Be("Member");
        result.Email.Should().Be("member@reg.com");
    }

    [Test]
    public async Task Register_DuplicateEmail_Returns400()
    {
        var slug = "dup-email-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug, "dup@test.com", "Password123!");

        // Try to register the same email again
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            tenantSlug = slug,
            email = "dup@test.com",
            fullName = "Duplicate",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Register_NonExistentTenant_Returns404()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            tenantSlug = "tenant-does-not-exist",
            email = "user@test.com",
            fullName = "User",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Register_InvalidEmail_Returns400()
    {
        var slug = "inv-email-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug);

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            tenantSlug = slug,
            email = "not-an-email",
            fullName = "User",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Register_ShortPassword_Returns400()
    {
        var slug = "short-pw-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug);

        var response = await Client.PostAsJsonAsync("/api/auth/register", new
        {
            tenantSlug = slug,
            email = "user@short.com",
            fullName = "User",
            password = "abc"  // too short
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── Login ─────────────────────────────────────────────────────────────────

    [Test]
    public async Task Login_ValidCredentials_ReturnsTokens()
    {
        var slug = "login-test-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug, "user@login.com", "Password123!");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            email = "user@login.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        result.Email.Should().Be("user@login.com");
    }

    [Test]
    public async Task Login_WrongPassword_Returns403()
    {
        var slug = "wrong-pass-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug, "user@wrong.com", "Password123!");

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            email = "user@wrong.com",
            password = "WrongPassword!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Login_NonExistentEmail_Returns403()
    {
        var slug = "no-user-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slug);

        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            email = "doesnotexist@test.com",
            password = "Password123!"
        });

        // Should return 403, not 404 — don't reveal whether the user exists
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task Login_WrongTenantForExistingUser_Returns404()
    {
        var slugA = "login-ta-" + Guid.NewGuid().ToString("N")[..6];
        var slugB = "login-tb-" + Guid.NewGuid().ToString("N")[..6];
        await RegisterTenantAsync(slugA, "user@loginta.com", "Password123!");
        await RegisterTenantAsync(slugB);

        // User exists in tenant A but tries to log in to tenant B
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slugB,
            email = "user@loginta.com",
            password = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Refresh Token ─────────────────────────────────────────────────────────

    [Test]
    public async Task RefreshToken_ValidToken_ReturnsNewTokens()
    {
        var slug = "refresh-test-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(slug, "refresh@test.com", "Password123!");

        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        result!.AccessToken.Should().NotBeNullOrEmpty();
        result.RefreshToken.Should().NotBeNullOrEmpty();
        // Token must be rotated — new refresh token should differ from original
        result.RefreshToken.Should().NotBe(auth.RefreshToken);
    }

    [Test]
    public async Task RefreshToken_TokenIsRotated_OldTokenCannotBeReused()
    {
        var slug = "reuse-test-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(slug, "reuse@test.com", "Password123!");

        // First refresh — succeeds and rotates
        var firstRefresh = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken
        });
        firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

        // Second refresh using the ORIGINAL token — must fail
        var secondRefresh = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken  // stale token
        });
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RefreshToken_NewTokenWorksCorrectly()
    {
        var slug = "new-token-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(slug, "newtoken@test.com", "Password123!");

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken
        });
        var newAuth = await refreshResponse.Content.ReadFromJsonAsync<AuthResult>();

        // The new access token must work on protected endpoints
        AuthorizeClient(newAuth!.AccessToken);
        var response = await Client.GetAsync("/api/tasks");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        ClearAuth();
    }

    [Test]
    public async Task RefreshToken_InvalidToken_Returns403()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = "invalid-access",
            refreshToken = "completely-invalid-refresh-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Revoke Token ──────────────────────────────────────────────────────────

    [Test]
    public async Task RevokeToken_ValidToken_Returns204()
    {
        var slug = "revoke-test-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(slug, "revoke@test.com", "Password123!");
        AuthorizeClient(auth.AccessToken);

        var response = await Client.PostAsJsonAsync("/api/auth/revoke", new
        {
            refreshToken = auth.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        ClearAuth();
    }

    [Test]
    public async Task RevokeToken_RevokedTokenCannotBeUsedForRefresh()
    {
        var slug = "revoke-use-" + Guid.NewGuid().ToString("N")[..6];
        var auth = await RegisterTenantAsync(slug, "revokeuse@test.com", "Password123!");
        AuthorizeClient(auth.AccessToken);

        // Revoke the token
        await Client.PostAsJsonAsync("/api/auth/revoke", new { refreshToken = auth.RefreshToken });
        ClearAuth();

        // Try to use the revoked token for refresh
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = auth.AccessToken,
            refreshToken = auth.RefreshToken
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Test]
    public async Task RevokeToken_Unauthenticated_Returns401()
    {
        ClearAuth();
        var response = await Client.PostAsJsonAsync("/api/auth/revoke", new
        {
            refreshToken = "some-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}