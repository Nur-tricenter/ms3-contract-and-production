using Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class CalendarioEnMemoria : IDeliveryCalendarClient
{
    private readonly List<ScheduleDeliveriesRequest> _calendariosCreados = new();

    public bool IsEnabled { get; set; } = true;

    public bool RechazarCalendario { get; set; }

    public bool FallarAlLlamar { get; set; }

    public List<ActiveDelivery> EntregasDeManana { get; } = new();

    public IReadOnlyList<ScheduleDeliveriesRequest> CalendariosCreados => _calendariosCreados;

    public Task ScheduleAsync(ScheduleDeliveriesRequest request, CancellationToken cancellationToken = default)
    {
        if (FallarAlLlamar)
            throw new HttpRequestException("MS4 no disponible.");

        if (RechazarCalendario)
            throw new DeliveryCalendarRejectedException(422, "La duración debe ser 15 o 30.");

        _calendariosCreados.Add(request);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ActiveDelivery>> GetActiveTomorrowAsync(DateOnly date, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ActiveDelivery>>(EntregasDeManana);
}
