using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.Identity;

namespace TaskFlow.Integration.Tests;

public class TaskFlowWebFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace SQL Server with InMemory
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(options =>
                options.UseInMemoryDatabase("TestDb_" + Guid.NewGuid()));

            // Replace Redis with in-memory cache
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<ICacheService>();
            services.AddSingleton<ICacheService, InMemoryCacheService>();

            // Replace hosted services (outbox processor) to avoid background noise
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();

            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }
}

// Simple in-memory cache for tests
public class InMemoryCacheService : ICacheService
{
    private readonly Dictionary<string, (object Value, DateTime? Expiry)> _store = new();

    public Task<T?> GetAsync<T>(string key)
    {
        if (_store.TryGetValue(key, out var entry))
        {
            if (entry.Expiry == null || entry.Expiry > DateTime.UtcNow)
                return Task.FromResult((T?)entry.Value);
            _store.Remove(key);
        }
        return Task.FromResult(default(T?));
    }

    public Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        _store[key] = (value!, expiry.HasValue ? DateTime.UtcNow.Add(expiry.Value) : null);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key) { _store.Remove(key); return Task.CompletedTask; }

    public Task RemoveByPatternAsync(string pattern)
    {
        var keys = _store.Keys.Where(k => k.Contains(pattern.Replace("*", ""))).ToList();
        foreach (var k in keys) _store.Remove(k);
        return Task.CompletedTask;
    }
}
