using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Contracting;

/// <summary>
/// Published to <c>contract-activated</c> when a contract is paid. MS4 consumes it and creates the
/// delivery calendar. Local copy of the kernel's <c>ContractActivated</c> (same wire shape); the swap
/// to the kernel type is still pending (ms3-gap-05).
/// </summary>
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
    public int DurationDays { get; init; }
    public DateOnly StartDate { get; init; }
}
