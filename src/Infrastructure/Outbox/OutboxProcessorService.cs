using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Domain.Interfaces;

namespace TaskFlow.Infrastructure.Outbox;

public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxProcessorService> _logger;
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    public OutboxProcessorService(IServiceProvider services, ILogger<OutboxProcessorService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox processor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessOutboxAsync();
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task ProcessOutboxAsync()
    {
        using var scope = _services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxRepository>();

        var messages = await outbox.GetUnprocessedAsync();

        foreach (var message in messages)
        {
            try
            {
                // Here you would dispatch to the actual handler
                // e.g. publish to Azure Service Bus, send email, etc.
                _logger.LogInformation("Processing outbox message {Id} of type {Type}", message.Id, message.Type);

                message.MarkProcessed();
                await outbox.UpdateAsync(message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                message.MarkFailed(ex.Message);
                await outbox.UpdateAsync(message);
            }
        }
    }
}
