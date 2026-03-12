using MediatR;
using TaskFlow.Application.Common.Interfaces;
using TaskFlow.Domain.Entities;
using TaskFlow.Domain.Events;
using Microsoft.Extensions.Logging;

namespace TaskFlow.Infrastructure.DependencyInjection;

public class DomainEventDispatcher : IDomainEventDispatcher
{
    private readonly IMediator _mediator;
    private readonly ILogger<DomainEventDispatcher> _logger;

    public DomainEventDispatcher(IMediator mediator, ILogger<DomainEventDispatcher> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events)
    {
        foreach (var domainEvent in events)
        {
            _logger.LogInformation("Domain event dispatched: {Event}", domainEvent.GetType().Name);
            // Extend here: publish to MediatR notifications, Azure Service Bus, etc.
            // await _mediator.Publish(domainEvent);
        }
    }
}
