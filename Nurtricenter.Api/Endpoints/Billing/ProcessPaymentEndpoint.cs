using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Billing;

namespace Nurtricenter.MS3.Api.Endpoints.Billing;

public sealed class ProcessPaymentEndpoint : Endpoint<ProcessPaymentRequest, PaymentResponse>
{
    private readonly IMediator _mediator;

    public ProcessPaymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/v1/billing/charge-plan");
        Description(d => d
            .WithTags("Billing")
            .WithSummary("Procesa un pago o cargo contra un contrato activo")
            .Produces<PaymentResponse>(200)
            .ProducesProblem(400)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessPaymentRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProcessPaymentCommand(req.ContractId, req.Amount, req.InvoiceNumber), ct);

        if (result.IsSuccess)
            await SendOkAsync(result.Value!, ct);
        else if (result.Error?.Type == Joseco.DDD.Core.Results.ErrorType.NotFound)
            await SendNotFoundAsync(ct);
        else
            ThrowError(result.Error!.Description);
    }
}
