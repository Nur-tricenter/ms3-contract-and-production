using System.Text.Json;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class ProcessPaymentCommandHandler : IRequestHandler<ProcessPaymentCommand, Result<PaymentResponse>>
{
    private readonly IContractRepository _contractRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutgoingEventRepository _outgoingEvents;

    public ProcessPaymentCommandHandler(
        IContractRepository contractRepository,
        IUnitOfWork unitOfWork,
        IOutgoingEventRepository outgoingEvents)
    {
        _contractRepository = contractRepository;
        _unitOfWork = unitOfWork;
        _outgoingEvents = outgoingEvents;
    }

    public async Task<Result<PaymentResponse>> Handle(ProcessPaymentCommand request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByIdAsync(request.ContractId);
        if (contract is null)
            return (Result<PaymentResponse>)Result.Failure(
                new Error("NOT_FOUND", $"Contract {request.ContractId} not found.", ErrorType.NotFound));

        var invoice = contract.ProcessPayment(request.Amount, request.InvoiceNumber);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Simulate MS4: create delivery calendar
        var payload = JsonSerializer.Serialize(new
        {
            contractId = contract.Id,
            contract.PatientId,
            contract.CatalogPlanId,
            invoice.InvoiceNumber,
            invoice.TotalAmount
        });

        await _outgoingEvents.AddAsync(
            new OutgoingIntegrationEvent("CreateSchedule", "MS4", payload), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        var response = new PaymentResponse(invoice.Id, "processed", DateTime.UtcNow);
        return Result<PaymentResponse>.Success(response);
    }
}
