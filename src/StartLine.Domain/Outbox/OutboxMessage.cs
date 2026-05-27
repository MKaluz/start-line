using StartLine.Domain.Common;

namespace StartLine.Domain.Outbox;

public class OutboxMessage : Entity
{
    public string Type { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create(string type, string payload) =>
        new()
        {
            Type = type,
            Payload = payload,
            CreatedAt = DateTimeOffset.UtcNow
        };
}
