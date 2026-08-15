using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Simulations;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class CreateContractCommandHandler : IRequestHandler<CreateContractCommand, Result<ContractResponse>>
{
    private readonly IContractRepository _contractRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISimulationService _sim;

    public CreateContractCommandHandler(
        IContractRepository contractRepository,
        IUnitOfWork unitOfWork,
        ISimulationService sim)
    {
        _contractRepository = contractRepository;
        _unitOfWork = unitOfWork;
        _sim = sim;
    }

    public async Task<Result<ContractResponse>> Handle(CreateContractCommand request, CancellationToken cancellationToken)
    {
        var patient = _sim.GetPatient(request.PatientId);
        if (patient is null)
            return Result.Failure<ContractResponse>(
                new Error("PATIENT_NOT_FOUND", $"Patient {request.PatientId} not found in MS1.", ErrorType.NotFound));

        var plan = _sim.GetPlan(request.CatalogPlanId);
        if (plan is null)
            return Result.Failure<ContractResponse>(
                new Error("PLAN_NOT_FOUND", $"Plan {request.CatalogPlanId} not found in MS2.", ErrorType.NotFound));

        var contract = Core.Aggregates.Contract.Create(request.PatientId, request.CatalogPlanId);

        await _contractRepository.AddAsync(contract);
        await _unitOfWork.CommitAsync(cancellationToken);

        var response = new ContractResponse(contract.Id, contract.PatientId, contract.CatalogPlanId, contract.Status.ToString(), contract.CreatedAt);
        return Result<ContractResponse>.Success(response);
    }
}
