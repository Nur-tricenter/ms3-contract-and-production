using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Infrastructure.Clinic;

/// <summary>Replaces the simulated patients with the real ones from MS1; everything else is delegated.</summary>
public sealed class ClinicSimulationService : ISimulationService
{
    private const string ActiveStatus = "active";

    private readonly ISimulationService _inner;
    private readonly IClinicClient _clinic;

    public ClinicSimulationService(ISimulationService inner, IClinicClient clinic)
    {
        _inner = inner;
        _clinic = clinic;
    }

    public SimPatient? GetPatient(Guid patientId)
    {
        var patient = _clinic.GetPatientAsync(patientId).GetAwaiter().GetResult();

        return patient is null
            ? null
            : new SimPatient(patient.Id, $"{patient.Nombre} {patient.Apellido}".Trim(), ActiveStatus);
    }

    public SimPlan? GetPlan(Guid planId)
        => _inner.GetPlan(planId);

    public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds)
        => _inner.GetPlanStructures(planIds);

    public string? GetRecipeName(Guid recipeId)
        => _inner.GetRecipeName(recipeId);

    public List<SimActiveCalendar> GetActiveCalendars(DateTime date)
        => _inner.GetActiveCalendars(date);
}
