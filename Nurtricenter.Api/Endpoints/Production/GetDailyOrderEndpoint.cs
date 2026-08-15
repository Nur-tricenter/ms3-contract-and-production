using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Application.Queries;

namespace Nurtricenter.MS3.Api.Endpoints.Production;

public sealed class GetDailyOrderEndpoint : Endpoint<GetDailyOrderRequest, DailyOrderResponse>
{
    private readonly IMediator _mediator;

    public GetDailyOrderEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/production/daily-order");
        Description(d => d
            .WithTags("Production")
            .WithSummary("Obtiene la orden de producción diaria para una fecha específica")
            .Produces<DailyOrderResponse>(200)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetDailyOrderRequest req, CancellationToken ct)
    {
        var rawDate = req.ProductionDate ?? DateTime.UtcNow.AddDays(1).Date;
        var utcDate = DateTime.SpecifyKind(rawDate, DateTimeKind.Utc);
        var result = await _mediator.Send(new GetDailyOrderQuery(utcDate), ct);

        if (result.IsSuccess)
            await SendOkAsync(result.Value!, ct);
        else
            await SendNotFoundAsync(ct);
    }
}
