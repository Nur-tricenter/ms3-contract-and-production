using FastEndpoints;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Api.Endpoints.Simulations;

public sealed class SimulateGetActiveCalendarsEndpoint : EndpointWithoutRequest<List<SimActiveCalendar>>
{
    private readonly ISimulationService _sim;

    public SimulateGetActiveCalendarsEndpoint(ISimulationService sim)
    {
        _sim = sim;
    }

    public override void Configure()
    {
        Get("/api/v1/sim/calendars/active-tomorrow");
        Description(d => d
            .WithTags("Simulations")
            .WithSummary("Simula la obtención de los calendarios activos para el día siguiente")
            .Produces<List<SimActiveCalendar>>(200));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var calendars = _sim.GetActiveCalendars(DateTime.UtcNow.AddDays(1).Date);
        await SendOkAsync(calendars, ct);
    }
}
