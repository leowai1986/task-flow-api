using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NUnit.Framework;
using TaskFlow.Application.Features.Auth.Commands;

namespace TaskFlow.Integration.Tests;

[TestFixture]
public class AuthIntegrationTests : BaseIntegrationTest
{
    [Test]
    public async Task RegisterTenant_ValidData_Returns201WithTokens()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "Acme Corp",
            slug = "acme-corp-" + Guid.NewGuid().ToString("N")[..6],
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
    }

    [Test]
    public async Task RegisterTenant_DuplicateSlug_Returns400()
    {
        var slug = "duplicate-" + Guid.NewGuid().ToString("N")[..6];

        await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "First", slug, adminEmail = "a@a.com",
            adminFullName = "A", adminPassword = "Password123!"
        });

        var response = await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "Second", slug, adminEmail = "b@b.com",
            adminFullName = "B", adminPassword = "Password123!"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

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
        result.RefreshToken.Should().NotBe(auth.RefreshToken); // rotated
    }

    [Test]
    public async Task RefreshToken_InvalidToken_Returns403()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh", new
        {
            accessToken = "invalid",
            refreshToken = "invalid-refresh-token"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
