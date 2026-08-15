using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Queries;

namespace Nurtricenter.MS3.Api.Endpoints.Billing;

public sealed class GetInvoiceEndpoint : EndpointWithoutRequest<InvoiceResponse>
{
    private readonly IMediator _mediator;

    public GetInvoiceEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/billing/{invoiceNumber}");
        Description(d => d
            .WithTags("Billing")
            .WithSummary("Obtiene una factura por su número de factura")
            .Produces<InvoiceResponse>(200)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var invoiceNumber = Route<string>("invoiceNumber")!;
        var result = await _mediator.Send(new GetInvoiceQuery(invoiceNumber), ct);

        if (result.IsSuccess)
            await SendOkAsync(result.Value!, ct);
        else
            await SendNotFoundAsync(ct);
    }
}
