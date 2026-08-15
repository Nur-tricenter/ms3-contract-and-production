using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Queries;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class GetContractQueryHandler : IRequestHandler<GetContractQuery, Result<ContractResponse>>
{
    private readonly IContractRepository _contractRepository;

    public GetContractQueryHandler(IContractRepository contractRepository)
    {
        _contractRepository = contractRepository;
    }

    public async Task<Result<ContractResponse>> Handle(GetContractQuery request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdAsync(request.ContractId);
        if (contract is null)
            return (Result<ContractResponse>)Result.Failure(
                new Error("NOT_FOUND", $"Contract {request.ContractId} not found.", ErrorType.NotFound));

        return Result<ContractResponse>.Success(
            new ContractResponse(contract.Id, contract.PatientId, contract.CatalogPlanId, contract.Status.ToString(), contract.CreatedAt));
    }
}
