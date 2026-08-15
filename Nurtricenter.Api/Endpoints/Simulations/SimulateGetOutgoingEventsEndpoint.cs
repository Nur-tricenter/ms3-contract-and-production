using FastEndpoints;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Api.Endpoints.Simulations;

public sealed class SimulateGetOutgoingEventsEndpoint : EndpointWithoutRequest<IReadOnlyList<OutgoingIntegrationEvent>>
{
    private readonly IOutgoingEventRepository _outgoingEvents;

    public SimulateGetOutgoingEventsEndpoint(IOutgoingEventRepository outgoingEvents)
    {
        _outgoingEvents = outgoingEvents;
    }

    public override void Configure()
    {
        Get("/api/v1/sim/outgoing/{eventType}");
        Description(d => d
            .WithTags("Simulations")
            .WithSummary("Simula la obtención de eventos de integración salientes por tipo de evento")
            .Produces<IReadOnlyList<OutgoingIntegrationEvent>>(200));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var eventType = Route<string>("eventType")!;
        var events = await _outgoingEvents.GetByTypeAsync(eventType, ct);
        await SendOkAsync(events, ct);
    }
}
