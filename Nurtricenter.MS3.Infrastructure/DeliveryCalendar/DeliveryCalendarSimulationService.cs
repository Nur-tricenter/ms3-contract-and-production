using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public sealed class DeliveryCalendarSimulationService : ISimulationService
{
    private readonly ISimulationService _inner;
    private readonly IDeliveryCalendarClient _deliveryCalendar;

    public DeliveryCalendarSimulationService(ISimulationService inner, IDeliveryCalendarClient deliveryCalendar)
    {
        _inner = inner;
        _deliveryCalendar = deliveryCalendar;
    }

    public SimPatient? GetPatient(Guid patientId)
        => _inner.GetPatient(patientId);

    public SimPlan? GetPlan(Guid planId)
        => _inner.GetPlan(planId);

    public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds)
        => _inner.GetPlanStructures(planIds);

    public string? GetRecipeName(Guid recipeId)
        => _inner.GetRecipeName(recipeId);

    public List<SimActiveCalendar> GetActiveCalendars(DateTime date)
    {
        var deliveries = _deliveryCalendar
            .GetActiveTomorrowAsync(DateOnly.FromDateTime(date))
            .GetAwaiter()
            .GetResult();

        return deliveries.Select(ToSimActiveCalendar).ToList();
    }

    public static Guid PackIdToGuid(int packId)
        => new(packId, 0, 0, new byte[8]);

    private static SimActiveCalendar ToSimActiveCalendar(ActiveDelivery delivery) => new(
        PackIdToGuid(delivery.PackId),
        delivery.PatientId,
        delivery.PlanId,
        delivery.Address,
        delivery.Latitude,
        delivery.Longitude,
        delivery.Hour?.ToString("HH:mm") ?? string.Empty);
}
