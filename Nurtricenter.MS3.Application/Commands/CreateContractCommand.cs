using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Contracts;

namespace Nurtricenter.MS3.Application.Commands;

public sealed record CreateContractCommand(
    Guid PatientId,
    Guid CatalogPlanId
) : IRequest<Result<ContractResponse>>;
