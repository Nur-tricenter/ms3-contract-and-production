using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Commands;

namespace Nurtricenter.MS3.Api.Endpoints.Contracts;

public sealed class CancelContractRequest
{
    public string Reason { get; set; } = string.Empty;
}

public sealed class CancelContractEndpoint : Endpoint<CancelContractRequest>
{
    private readonly IMediator _mediator;

    public CancelContractEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/v1/contracts/{id:guid}/cancel");
        Description(d => d
            .WithTags("Contracts")
            .WithSummary("Cancela un contrato existente especificando el motivo")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancelContractRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _mediator.Send(new CancelContractCommand(id, req.Reason), ct);

        if (result.IsSuccess)
            await SendNoContentAsync(ct);
        else if (result.Error?.Code == "NOT_FOUND")
            await SendNotFoundAsync(ct);
        else
            ThrowError(result.Error!.Description);
    }
}
