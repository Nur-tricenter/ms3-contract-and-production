using FastEndpoints;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Dtos.Contracts;

namespace Nurtricenter.MS3.Api.Endpoints.Contracts;

public sealed class CreateContractEndpoint : Endpoint<CreateContractRequest, ContractResponse>
{
    private readonly IMediator _mediator;

    public CreateContractEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/v1/contracts");
        Description(d => d
            .WithTags("Contracts")
            .WithSummary("Crea un nuevo contrato para un paciente con un plan nutricional")
            .Produces<ContractResponse>(201)
            .ProducesProblem(400)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateContractRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(new CreateContractCommand(req.PatientId, req.CatalogPlanId), ct);

        if (result.IsSuccess)
        {
            await SendCreatedAtAsync<GetContractEndpoint>(
                new { id = result.Value!.Id }, result.Value, cancellation: ct);
        }
        else if (result.Error?.Type == Joseco.DDD.Core.Results.ErrorType.NotFound)
            await SendNotFoundAsync(ct);
        else
            ThrowError(result.Error!.Description);
    }
}
