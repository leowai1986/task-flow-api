using System.Net.Http.Headers;
using System.Net.Http.Json;
using NUnit.Framework;
using TaskFlow.Application.Features.Auth.Commands;

namespace TaskFlow.Integration.Tests;

[TestFixture]
public abstract class BaseIntegrationTest
{
    protected TaskFlowWebFactory Factory = null!;
    protected HttpClient Client = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        Factory = new TaskFlowWebFactory();
        Client = Factory.CreateClient();
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        Client.Dispose();
        Factory.Dispose();
    }

    protected async Task<AuthResult> RegisterTenantAsync(
        string slug = "test-corp",
        string email = "admin@test.com",
        string password = "Password123!")
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register-tenant", new
        {
            name = "Test Corp",
            slug,
            adminEmail = email,
            adminFullName = "Test Admin",
            adminPassword = password
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    protected async Task<AuthResult> LoginAsync(string slug, string email, string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/login", new
        {
            tenantSlug = slug,
            email,
            password
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResult>())!;
    }

    protected void AuthorizeClient(string token) =>
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    protected void ClearAuth() =>
        Client.DefaultRequestHeaders.Authorization = null;
}
