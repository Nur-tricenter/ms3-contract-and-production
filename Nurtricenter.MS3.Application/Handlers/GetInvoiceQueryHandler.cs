using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Queries;
using Nurtricenter.MS3.Application.Simulations;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class GetInvoiceQueryHandler : IRequestHandler<GetInvoiceQuery, Result<InvoiceResponse>>
{
    private readonly IContractRepository _contractRepository;
    private readonly ISimulationService _sim;

    public GetInvoiceQueryHandler(IContractRepository contractRepository, ISimulationService sim)
    {
        _contractRepository = contractRepository;
        _sim = sim;
    }

    public async Task<Result<InvoiceResponse>> Handle(GetInvoiceQuery request, CancellationToken cancellationToken)
    {
        var contract = await _contractRepository.GetByInvoiceNumberAsync(request.InvoiceNumber, cancellationToken);
        if (contract?.Invoice is null)
            return (Result<InvoiceResponse>)Result.Failure(
                new Error("NOT_FOUND", $"Invoice '{request.InvoiceNumber}' not found.", ErrorType.NotFound));

        var invoice = contract.Invoice;
        var plan = _sim.GetPlan(contract.CatalogPlanId);

        return Result<InvoiceResponse>.Success(new InvoiceResponse(
            invoice.Id,
            contract.PatientId,
            plan?.PlanName ?? "Unknown Plan",
            invoice.TotalAmount,
            contract.CreatedAt,
            invoice.IsPaid));
    }
}
