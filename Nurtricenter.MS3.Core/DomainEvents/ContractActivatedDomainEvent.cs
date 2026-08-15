using Joseco.DDD.Core.Abstractions;

namespace Nurtricenter.MS3.Core.DomainEvents;

public sealed record ContractActivatedDomainEvent(
    Guid ContractId,
    Guid PatientId,
    Guid CatalogPlanId
) : DomainEvent;
