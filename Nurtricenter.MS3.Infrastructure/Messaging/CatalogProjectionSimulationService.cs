using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Infrastructure.Messaging;

public sealed class CatalogProjectionSimulationService : ISimulationService
{
    private readonly ISimulationService _inner;
    private readonly ApplicationDbContext _context;

    public CatalogProjectionSimulationService(ISimulationService inner, ApplicationDbContext context)
    {
        _inner = inner;
        _context = context;
    }

    public SimPatient? GetPatient(Guid patientId)
        => _inner.GetPatient(patientId);

    public SimPlan? GetPlan(Guid planId)
    {
        var simulated = _inner.GetPlan(planId);
        if (simulated is not null)
            return simulated;

        var projected = _context.CatalogPlans.Find(planId);
        return projected is null
            ? null
            : new SimPlan(projected.PlanId, projected.Name, 0m, projected.TotalDays);
    }

    public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds)
        => _inner.GetPlanStructures(planIds);

    public List<SimActiveCalendar> GetActiveCalendars(DateTime date)
        => _inner.GetActiveCalendars(date);

    public string? GetRecipeName(Guid recipeId)
        => _inner.GetRecipeName(recipeId) ?? _context.CatalogRecipes.Find(recipeId)?.Name;
}
