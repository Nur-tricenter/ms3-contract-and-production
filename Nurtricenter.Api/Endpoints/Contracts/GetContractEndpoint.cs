using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Application.Queries;

namespace Nurtricenter.MS3.Api.Endpoints.Contracts;

public sealed class GetContractEndpoint : EndpointWithoutRequest<ContractResponse>
{
    private readonly IMediator _mediator;

    public GetContractEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/contracts/{id:guid}");
        Description(d => d
            .WithTags("Contracts")
            .WithSummary("Obtiene un contrato por su identificador único")
            .Produces<ContractResponse>(200)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _mediator.Send(new GetContractQuery(id), ct);

        if (result.IsSuccess)
            await SendOkAsync(result.Value!, ct);
        else
            await SendNotFoundAsync(ct);
    }
}
