using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Infrastructure.DependencyInjection;

public class EntityEventCollector : IEntityEventCollector
{
    private readonly AppDbContext _db;

    public EntityEventCollector(AppDbContext db) => _db = db;

    public IReadOnlyList<IDomainEvent> CollectAndClear()
    {
        var entities = _db.ChangeTracker
            .Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Any())
            .ToList();

        var events = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        foreach (var entity in entities)
            entity.ClearDomainEvents();

        return events;
    }
}

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IPublisher _publisher;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IPublisher publisher, ILogger<DomainEventDispatcher> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        foreach (var domainEvent in events)
        {
            _logger.LogInformation("Dispatching domain event: {Event}", domainEvent.GetType().Name);

            if (domainEvent is INotification notification)
                await _publisher.Publish(notification);
        }
    }
}