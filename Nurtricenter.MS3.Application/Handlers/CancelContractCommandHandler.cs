using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Interfaces;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class CancelContractCommandHandler : IRequestHandler<CancelContractCommand, Result>
{
    private readonly IContractRepository _contractRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelContractCommandHandler(
        IContractRepository contractRepository,
        IUnitOfWork unitOfWork)
    {
        _contractRepository = contractRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(CancelContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdAsync(request.ContractId);

        if (contract is null)
            return Result.Failure(
                new Error("NOT_FOUND", $"Contract {request.ContractId} not found.", ErrorType.NotFound));

        contract.CancelContract(request.Reason);

        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
