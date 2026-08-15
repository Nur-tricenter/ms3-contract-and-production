using FastEndpoints;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Api.Endpoints.Simulations;

public sealed class SimulateGetPatientEndpoint : EndpointWithoutRequest<SimPatient>
{
    private readonly ISimulationService _sim;

    public SimulateGetPatientEndpoint(ISimulationService sim)
    {
        _sim = sim;
    }

    public override void Configure()
    {
        Get("/api/v1/sim/patients/{id:guid}");
        Description(d => d
            .WithTags("Simulations")
            .WithSummary("Simula la obtención de un paciente por su identificador único")
            .Produces<SimPatient>(200)
            .ProducesProblem(404));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var patient = _sim.GetPatient(id);

        if (patient is not null)
            await SendOkAsync(patient, ct);
        else
            await SendNotFoundAsync(ct);
    }
}
