using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Application.Common.Services;
using TaskFlow.Domain.Interfaces;
using TaskFlow.Infrastructure.Cache;
using TaskFlow.Infrastructure.Data;
using TaskFlow.Infrastructure.Identity;
using TaskFlow.Infrastructure.Outbox;
using TaskFlow.Infrastructure.Repositories;

namespace TaskFlow.Infrastructure.DependencyInjection;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        var redisConn = config.GetConnectionString("Redis")!;
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConn));
        services.AddStackExchangeRedisCache(options => options.Configuration = redisConn);

        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();
        services.AddScoped<IOutboxRepository, OutboxRepository>();

        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());

        services.AddScoped<ICacheService, RedisCacheService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        services.AddHostedService<OutboxProcessorService>();

        return services;
    }
}
