using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Contracting;

public record ContractActivated : IntegrationMessage
{
    public ContractActivated()
    {
    }

    public ContractActivated(string? correlationId, string? source)
        : base(correlationId, source)
    {
    }

    public string EventType => "Contract.Activated";

    public Guid ContractId { get; init; }
    public Guid PatientId { get; init; }
    public Guid CatalogPlanId { get; init; }
    public string InvoiceNumber { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}
