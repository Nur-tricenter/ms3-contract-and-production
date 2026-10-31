using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Messaging.Consumers;

namespace Nurtricenter.MS3.Infrastructure.Messaging;

/// <summary>
/// Serves the patients and the next-day deliveries from MS3's own projections of MS1's
/// <c>PatientPublished</c> and MS4's <c>ProductionOrderReady</c>, so no call to either service is made.
/// Everything else is delegated.
/// </summary>
/// <remarks>
/// Enabled by <c>Projections:Enabled</c> (the platform); standalone keeps the in-memory simulation. When
/// enabled an unknown patient is simply not found (the existing <c>PATIENT_NOT_FOUND</c> 404), and a date
/// MS4 has not published an order for has no deliveries.
/// </remarks>
public sealed class ProjectedDataSimulationService : ISimulationService
{
    private const string ActiveStatus = "active";

    private readonly ISimulationService _inner;
    private readonly ApplicationDbContext _context;

    public ProjectedDataSimulationService(ISimulationService inner, ApplicationDbContext context)
    {
        _inner = inner;
        _context = context;
    }

    public SimPatient? GetPatient(Guid patientId)
    {
        var patient = _context.ClinicPatients.Find(patientId);

        return patient is null
            ? null
            : new SimPatient(patient.PatientId, patient.FullName, ActiveStatus, patient.Phone);
    }

    public List<SimActiveCalendar> GetActiveCalendars(DateTime date)
    {
        var deliveryDate = DateOnly.FromDateTime(date);

        return _context.ScheduledDeliveries
            .Where(d => d.DeliveryDate == deliveryDate)
            .OrderBy(d => d.DeliveryDayId)
            .AsEnumerable()
            .Select(d => new SimActiveCalendar(
                PackIdToGuid(d.DeliveryDayId),
                d.PatientId,
                d.PlanId,
                d.Address,
                d.Latitude ?? 0,
                d.Longitude ?? 0,
                d.Hour ?? string.Empty,
                ProductionOrderReadyConsumer.ReadMenu(d.MenuJson)
                    .OrderBy(item => item.MealTimeOrder)
                    .Select(item => new SimMenuItem(item.MealTime, item.RecipeId, item.RecipeName, item.Rations))
                    .ToList(),
                d.DeliveryDayId))
            .ToList();
    }

    /// <summary>The package id MS3 uses for an MS4 delivery day: the day id packed into a Guid.</summary>
    public static Guid PackIdToGuid(int packId)
        => new(packId, 0, 0, new byte[8]);

    public SimPlan? GetPlan(Guid planId)
        => _inner.GetPlan(planId);

    public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds)
        => _inner.GetPlanStructures(planIds);

    public string? GetRecipeName(Guid recipeId)
        => _inner.GetRecipeName(recipeId);
}
