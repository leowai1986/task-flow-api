using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Exceptions;

namespace TaskFlow.Application.Common.Behaviors;

// Validation Pipeline Behavior
public class ValidationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IEnumerable<IValidator<TRequest>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TRequest>> validators)
        => _validators = validators;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        if (!_validators.Any()) return await next();

        var context = new ValidationContext<TRequest>(request);
        var failures = _validators
            .Select(v => v.Validate(context))
            .SelectMany(r => r.Errors)
            .Where(e => e != null)
            .ToList();

        if (failures.Any())
            throw new DomainException(string.Join("; ", failures.Select(f => f.ErrorMessage)));

        return await next();
    }
}

// Logging Pipeline Behavior
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
        => _logger = logger;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        _logger.LogInformation("Handling {Request}", name);
        var response = await next();
        _logger.LogInformation("Handled {Request}", name);
        return response;
    }
}

// Domain Events Dispatcher Behavior
public class DomainEventBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly TaskFlow.Application.Common.Interfaces.IDomainEventDispatcher _dispatcher;

    public DomainEventBehavior(TaskFlow.Application.Common.Interfaces.IDomainEventDispatcher dispatcher)
        => _dispatcher = dispatcher;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var response = await next();
        // Domain events dispatched after successful handler execution
        // The dispatcher picks them up from the EF ChangeTracker
        await _dispatcher.DispatchAsync(Enumerable.Empty<Domain.Entities.IDomainEvent>());
        return response;
    }
}
