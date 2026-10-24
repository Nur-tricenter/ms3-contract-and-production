namespace Nurtricenter.MS3.Core.Simulations;

public sealed class OutgoingIntegrationEvent
{
    public const string PendingStatus = "Simulated";
    public const string PublishedStatus = "Published";
    public const string UnroutableStatus = "Unroutable";
    public const string RejectedStatus = "Rejected";

    public Guid Id { get; private set; }
    public string EventType { get; private set; }
    public string TargetService { get; private set; }
    public string Payload { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? PublishedAt { get; private set; }

    private OutgoingIntegrationEvent() { }

    public OutgoingIntegrationEvent(string eventType, string targetService, string payload)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        TargetService = targetService;
        Payload = payload;
        Status = PendingStatus;
        CreatedAt = DateTime.UtcNow;
    }

    public bool IsPending => Status == PendingStatus;

    public void MarkAsPublished(DateTime publishedAt)
    {
        if (!IsPending)
            throw new InvalidOperationException($"Event {Id} is '{Status}' and cannot be published again.");

        Status = PublishedStatus;
        PublishedAt = publishedAt;
    }

    public void MarkAsRejected()
    {
        if (!IsPending)
            throw new InvalidOperationException($"Event {Id} is '{Status}' and cannot be marked as rejected.");

        Status = RejectedStatus;
    }

    public void MarkAsUnroutable()
    {
        if (!IsPending)
            throw new InvalidOperationException($"Event {Id} is '{Status}' and cannot be marked as unroutable.");

        Status = UnroutableStatus;
    }
}
