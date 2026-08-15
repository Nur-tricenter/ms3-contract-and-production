using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Production;

namespace Nurtricenter.MS3.Api.Endpoints.Production;

public sealed class GenerateLabelEndpoint : Endpoint<GenerateLabelRequest>
{
    private readonly IMediator _mediator;

    public GenerateLabelEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/v1/production/packages/label");
        Description(d => d
            .WithTags("Production")
            .WithSummary("Genera la etiqueta para un paquete de producción")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenerateLabelRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new GenerateLabelCommand(req.PackageId, req.PatientId), ct);

        if (result.IsSuccess)
            await SendNoContentAsync(ct);
        else if (result.Error?.Code == "NOT_FOUND")
            await SendNotFoundAsync(ct);
        else
            ThrowError(result.Error!.Description);
    }
}
