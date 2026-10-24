namespace Nurtricenter.MS3.Infrastructure.Data.ReadModels;

public sealed class InboxMessage
{
    private InboxMessage() { }

    public InboxMessage(Guid messageId, string messageType, string? correlationId, DateTimeOffset messageCreatedAt)
    {
        MessageId = messageId;
        MessageType = messageType;
        CorrelationId = correlationId;
        MessageCreatedAt = messageCreatedAt.UtcDateTime;
        ProcessedOn = DateTime.UtcNow;
    }

    public Guid MessageId { get; private set; }
    public string MessageType { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public DateTime MessageCreatedAt { get; private set; }
    public DateTime ProcessedOn { get; private set; }
}
