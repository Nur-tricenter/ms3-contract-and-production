namespace Nurtricenter.MS3.Application.Dtos.Contracts;

public sealed record CreateContractRequest(
    Guid PatientId,
    Guid CatalogPlanId
);

public sealed record ContractResponse(
    Guid Id,
    Guid PatientId,
    Guid CatalogPlanId,
    string Status,
    DateTime CreatedAt
);

public sealed record CancelContractRequest(
    string Reason
);
