using Joseco.DDD.Core.Abstractions;

namespace Nurtricenter.MS3.Core.DomainEvents;

public sealed record ContractCanceledDomainEvent(
    Guid ContractId,
    Guid PatientId,
    string Reason
) : DomainEvent;
