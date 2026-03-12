namespace TaskFlow.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload) => new()
    {
        Id = Guid.NewGuid(),
        Type = type,
        Payload = payload,
        CreatedAt = DateTime.UtcNow
    };

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;

    public void MarkFailed(string error)
    {
        Error = error;
        RetryCount++;
    }
}
