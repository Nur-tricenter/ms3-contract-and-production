using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class ProcessControlChargeCommandHandler : IRequestHandler<ProcessControlChargeCommand, Result<ControlChargeResponse>>
{
    private readonly IControlChargeRepository _controlChargeRepository;
    private readonly IContractRepository _contractRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessControlChargeCommandHandler(
        IControlChargeRepository controlChargeRepository,
        IContractRepository contractRepository,
        IUnitOfWork unitOfWork)
    {
        _controlChargeRepository = controlChargeRepository;
        _contractRepository = contractRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ControlChargeResponse>> Handle(ProcessControlChargeCommand request, CancellationToken cancellationToken)
    {
        // MS3 checks internally if patient has an active catering contract
        var existingContract = await _contractRepository.GetByPatientIdAsync(request.PatientId, cancellationToken);
        if (existingContract is { Status: Core.Enums.ContractStatus.Active })
            return (Result<ControlChargeResponse>)Result<ControlChargeResponse>.Failure(
                new Error("ACTIVE_CONTRACT", "Patient has an active catering contract. Control charge not applicable.", ErrorType.Conflict));

        var controlCharge = ControlCharge.ProcessControlCharge(
            request.PatientId, request.ControlPolicyId, request.Amount, request.InvoiceNumber);

        await _controlChargeRepository.AddAsync(controlCharge);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result<ControlChargeResponse>.Success(
            new ControlChargeResponse(controlCharge.InvoiceNumber, "paid", controlCharge.Amount));
    }
}
