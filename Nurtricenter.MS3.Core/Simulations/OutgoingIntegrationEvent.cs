namespace Nurtricenter.MS3.Core.Simulations;

public sealed class OutgoingIntegrationEvent
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; }
    public string TargetService { get; private set; }
    public string Payload { get; private set; }
    public string Status { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private OutgoingIntegrationEvent() { }

    public OutgoingIntegrationEvent(string eventType, string targetService, string payload)
    {
        Id = Guid.NewGuid();
        EventType = eventType;
        TargetService = targetService;
        Payload = payload;
        Status = "Simulated";
        CreatedAt = DateTime.UtcNow;
    }
}
