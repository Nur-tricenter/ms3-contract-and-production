using FastEndpoints;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Api.Endpoints.Simulations;

public sealed class SimulateGetPlanEndpoint : EndpointWithoutRequest<SimPlan>
{
    private readonly ISimulationService _sim;

    public SimulateGetPlanEndpoint(ISimulationService sim)
    {
        _sim = sim;
    }

    public override void Configure()
    {
        Get("/api/v1/sim/catalog/plans/{id:guid}");
        Description(d => d
            .WithTags("Simulations")
            .WithSummary("Simula la obtención de un plan del catálogo por su identificador único")
            .Produces<SimPlan>(200)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var plan = _sim.GetPlan(id);

        if (plan is not null)
            await SendOkAsync(plan, ct);
        else
            await SendNotFoundAsync(ct);
    }
}
