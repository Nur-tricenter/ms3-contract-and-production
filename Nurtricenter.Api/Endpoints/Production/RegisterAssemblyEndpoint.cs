using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Production;

namespace Nurtricenter.MS3.Api.Endpoints.Production;

public sealed class RegisterAssemblyEndpoint : Endpoint<RegisterAssemblyRequest>
{
    private readonly IMediator _mediator;

    public RegisterAssemblyEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/v1/production/packages/assemble");
        Description(d => d
            .WithTags("Production")
            .WithSummary("Registra el ensamblaje de un paquete por parte del personal")
            .Produces(204)
            .ProducesProblem(400)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(RegisterAssemblyRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new RegisterAssemblyCommand(req.PackageId, req.StaffId), ct);

        if (result.IsSuccess)
            await SendNoContentAsync(ct);
        else if (result.Error?.Code == "NOT_FOUND")
            await SendNotFoundAsync(ct);
        else
            ThrowError(result.Error!.Description);
    }
}
