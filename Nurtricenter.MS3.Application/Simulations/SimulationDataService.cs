using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.Application.Simulations;

public sealed class SimulationDataService : ISimulationService
{
    private static readonly List<SimPatient> Patients =
    [
        new(Guid.Parse("00000000-0000-0000-0000-000000000001"), "Juan Perez", "active"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000002"), "Juana Suarez", "active"),
        new(Guid.Parse("00000000-0000-0000-0000-000000000003"), "Luis Ferrufino", "active")
    ];

    private static readonly List<SimPlan> Plans =
    [
        new(Guid.Parse("10000000-0000-0000-0000-000000000001"), "Premium Month Plan", 550.00m, 30),
        new(Guid.Parse("10000000-0000-0000-0000-000000000002"), "Basic Month Plan", 350.00m, 30),
        new(Guid.Parse("10000000-0000-0000-0000-000000000003"), "Standard Plan", 150.00m, 15)
    ];

    private static readonly Dictionary<Guid, SimPlanStructure> PlanStructures = new()
    {
        [Guid.Parse("10000000-0000-0000-0000-000000000001")] = new(
            Guid.Parse("10000000-0000-0000-0000-000000000001"),
            [
                new("desayuno", Guid.Parse("20000000-0000-0000-0000-000000000001")),
                new("almuerzo", Guid.Parse("20000000-0000-0000-0000-000000000002")),
                new("cena", Guid.Parse("20000000-0000-0000-0000-000000000003"))
            ]
        ),
        [Guid.Parse("10000000-0000-0000-0000-000000000002")] = new(
            Guid.Parse("10000000-0000-0000-0000-000000000002"),
            [
                new("desayuno", Guid.Parse("20000000-0000-0000-0000-000000000004")),
                new("almuerzo", Guid.Parse("20000000-0000-0000-0000-000000000005"))
            ]
        ),
        [Guid.Parse("10000000-0000-0000-0000-000000000003")] = new(
            Guid.Parse("10000000-0000-0000-0000-000000000003"),
            [
                new("almuerzo", Guid.Parse("20000000-0000-0000-0000-000000000001")),
                new("cena", Guid.Parse("20000000-0000-0000-0000-000000000003"))
            ]
        )
    };

    private static readonly List<SimActiveCalendar> ActiveCalendars =
    [
        new(Guid.Parse("30000000-0000-0000-0000-000000000001"), Guid.Parse("00000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000001"), "Av. Las Flores #123", -12.0464, -77.0428, "10:00"),
        new(Guid.Parse("30000000-0000-0000-0000-000000000002"), Guid.Parse("00000000-0000-0000-0000-000000000002"), Guid.Parse("10000000-0000-0000-0000-000000000001"), "Calle Los Olivos #456", -12.0450, -77.0435, "08:00"),
        new(Guid.Parse("30000000-0000-0000-0000-000000000003"), Guid.Parse("00000000-0000-0000-0000-000000000001"), Guid.Parse("10000000-0000-0000-0000-000000000002"), "Barrio Puno #789", -12.0470, -77.0410, "09:00")
    ];

    public SimPatient? GetPatient(Guid patientId)
        => Patients.FirstOrDefault(p => p.PatientId == patientId);

    public SimPlan? GetPlan(Guid planId)
        => Plans.FirstOrDefault(p => p.PlanId == planId);

    public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds)
        => planIds
            .Where(id => PlanStructures.ContainsKey(id))
            .Select(id => PlanStructures[id])
            .ToList();

    public List<SimActiveCalendar> GetActiveCalendars(DateTime date)
        => ActiveCalendars;
}
