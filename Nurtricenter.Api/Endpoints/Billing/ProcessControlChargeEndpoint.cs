using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Billing;

namespace Nurtricenter.MS3.Api.Endpoints.Billing;

public sealed class ProcessControlChargeEndpoint : Endpoint<ProcessControlChargeRequest, ControlChargeResponse>
{
    private readonly IMediator _mediator;

    public ProcessControlChargeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/v1/billing/charge-control");
        Description(d => d
            .WithTags("Billing")
            .WithSummary("Procesa un cargo de control (copago) contra una póliza")
            .Produces<ControlChargeResponse>(201)
            .ProducesProblem(400)
            .ProducesProblem(409));
        AllowAnonymous();
    }

    public override async Task HandleAsync(ProcessControlChargeRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new ProcessControlChargeCommand(req.PatientId, req.ControlPolicyId, req.Amount, req.InvoiceNumber), ct);

        if (result.IsSuccess)
            await SendAsync(result.Value!, 201, ct);
        else if (result.Error?.Type == Joseco.DDD.Core.Results.ErrorType.Conflict)
            ThrowError(result.Error!.Description, 409);
        else
            ThrowError(result.Error!.Description);
    }
}
