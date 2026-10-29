using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Production;

public record PackageValidated : IntegrationMessage
{
    public PackageValidated()
    {
    }

    public PackageValidated(string? correlationId, string? source)
        : base(correlationId, source)
    {
    }

    public string EventType => "Package.Validated";

    public Guid PackageId { get; init; }
    public Guid PatientId { get; init; }
    public string PatientName { get; init; } = string.Empty;
    public Guid ProductionOrderId { get; init; }
    public Guid CatalogPlanId { get; init; }
    public DateTime ReleasedAt { get; init; }
    public DateOnly DeliveryDate { get; init; }
    public string DeliveryAddress { get; init; } = string.Empty;
    public string TrackingNumber { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
}
