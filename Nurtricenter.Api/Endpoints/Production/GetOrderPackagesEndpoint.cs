using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Application.Queries;

namespace Nurtricenter.MS3.Api.Endpoints.Production;

public sealed class GetOrderPackagesEndpoint : EndpointWithoutRequest<ProductionOrderPackagesResponse>
{
    private readonly IMediator _mediator;

    public GetOrderPackagesEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/production/orders/{id:guid}/packages");
        Description(d => d
            .WithTags("Production")
            .WithSummary("Lists the packages of a production order with their status, label and validation (404 if the order has no packages)")
            .Produces<ProductionOrderPackagesResponse>(200)
            .ProducesProblem(404));
        // Same convention as every other MS3 endpoint today (gap X-11: MS3 does not validate JWTs yet).
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var result = await _mediator.Send(new GetOrderPackagesQuery(id), ct);

        if (result.IsSuccess)
            await SendOkAsync(result.Value!, ct);
        else
            await SendNotFoundAsync(ct);
    }
}
