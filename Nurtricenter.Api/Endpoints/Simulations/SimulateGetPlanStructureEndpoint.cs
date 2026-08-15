using FastEndpoints;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Api.Endpoints.Simulations;

public sealed class GetPlanStructureRequest
{
    public List<Guid> CatalogPlanIds { get; set; } = [];
}

public sealed class GetPlanStructureResponse
{
    public List<SimPlanStructure> PlansStructure { get; set; } = [];
}

public sealed class SimulateGetPlanStructureEndpoint : Endpoint<GetPlanStructureRequest, GetPlanStructureResponse>
{
    private readonly ISimulationService _sim;

    public SimulateGetPlanStructureEndpoint(ISimulationService sim)
    {
        _sim = sim;
    }

    public override void Configure()
    {
        Post("/api/v1/sim/catalog/plans/structure");
        Description(d => d
            .WithTags("Simulations")
            .WithSummary("Simula la obtención de la estructura de múltiples planes del catálogo")
            .Produces<GetPlanStructureResponse>(200));
        AllowAnonymous();
    }

    public override async Task HandleAsync(GetPlanStructureRequest req, CancellationToken ct)
    {
        var structures = _sim.GetPlanStructures(req.CatalogPlanIds);
        await SendOkAsync(new GetPlanStructureResponse { PlansStructure = structures }, ct);
    }
}
