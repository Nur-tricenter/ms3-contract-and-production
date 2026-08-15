using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Application.Simulations;

public interface ISimulationService
{
    SimPatient? GetPatient(Guid patientId);
    SimPlan? GetPlan(Guid planId);
    List<SimPlanStructure> GetPlanStructures(List<Guid> planIds);
    List<SimActiveCalendar> GetActiveCalendars(DateTime date);
}
