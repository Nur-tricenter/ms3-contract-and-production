using FluentAssertions;
using Nurtricenter.MS3.Application.Simulations;

namespace Nurtricenter.MS3.Tests.Application;

public sealed class SimulationDataServiceTests
{
    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PacienteJuanaSuarez = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid PacienteLuisFerrufino = Guid.Parse("00000000-0000-0000-0000-000000000003");

    private static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanBasicMensual = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid PlanEstandar = Guid.Parse("10000000-0000-0000-0000-000000000003");

    private readonly SimulationDataService _service = new();

    [Fact]
    public void GetPatient_devuelve_a_Juan_Perez_para_su_id_determinista()
    {
        var paciente = _service.GetPatient(PacienteJuanPerez);

        paciente.Should().NotBeNull();
        paciente!.PatientId.Should().Be(PacienteJuanPerez);
        paciente.FullName.Should().Be("Juan Perez");
        paciente.ClinicalStatus.Should().Be("active");
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000002", "Juana Suarez")]
    [InlineData("00000000-0000-0000-0000-000000000003", "Luis Ferrufino")]
    public void GetPatient_devuelve_el_paciente_esperado_para_cada_id_determinista(string id, string nombreEsperado)
    {
        var paciente = _service.GetPatient(Guid.Parse(id));

        paciente.Should().NotBeNull();
        paciente!.FullName.Should().Be(nombreEsperado);
    }

    [Fact]
    public void GetPatient_devuelve_null_para_un_id_desconocido()
    {
        var paciente = _service.GetPatient(Guid.NewGuid());

        paciente.Should().BeNull();
    }

    [Fact]
    public void GetPlan_devuelve_el_plan_premium_con_su_costo_y_duracion()
    {
        var plan = _service.GetPlan(PlanPremiumMensual);

        plan.Should().NotBeNull();
        plan!.PlanId.Should().Be(PlanPremiumMensual);
        plan.PlanName.Should().Be("Premium Month Plan");
        plan.Cost.Should().Be(550.00m);
        plan.DurationDays.Should().Be(30);
    }

    [Theory]
    [InlineData("10000000-0000-0000-0000-000000000002", "Basic Month Plan", 350.00, 30)]
    [InlineData("10000000-0000-0000-0000-000000000003", "Standard Plan", 150.00, 15)]
    public void GetPlan_devuelve_el_plan_esperado_para_cada_id_determinista(
        string id, string nombreEsperado, double costoEsperado, int duracionEsperada)
    {
        var plan = _service.GetPlan(Guid.Parse(id));

        plan.Should().NotBeNull();
        plan!.PlanName.Should().Be(nombreEsperado);
        plan.Cost.Should().Be((decimal)costoEsperado);
        plan.DurationDays.Should().Be(duracionEsperada);
    }

    [Fact]
    public void GetPlan_devuelve_null_para_un_id_desconocido()
    {
        var plan = _service.GetPlan(Guid.NewGuid());

        plan.Should().BeNull();
    }

    [Fact]
    public void GetPlanStructures_devuelve_la_estructura_completa_de_un_plan_conocido()
    {
        var estructuras = _service.GetPlanStructures([PlanPremiumMensual]);

        estructuras.Should().ContainSingle();
        estructuras[0].CatalogPlanId.Should().Be(PlanPremiumMensual);
        estructuras[0].Meals.Should().HaveCount(3);
        estructuras[0].Meals.Select(m => m.MealTime).Should().Equal("desayuno", "almuerzo", "cena");
    }

    [Fact]
    public void GetPlanStructures_devuelve_una_estructura_por_cada_plan_conocido_solicitado()
    {
        var estructuras = _service.GetPlanStructures([PlanPremiumMensual, PlanBasicMensual, PlanEstandar]);

        estructuras.Should().HaveCount(3);
        estructuras.Select(e => e.CatalogPlanId).Should().BeEquivalentTo(
            [PlanPremiumMensual, PlanBasicMensual, PlanEstandar]);
    }

    [Fact]
    public void GetPlanStructures_ignora_los_ids_de_plan_desconocidos()
    {
        var idDesconocido = Guid.NewGuid();

        var estructuras = _service.GetPlanStructures([PlanBasicMensual, idDesconocido]);

        estructuras.Should().ContainSingle();
        estructuras[0].CatalogPlanId.Should().Be(PlanBasicMensual);
    }

    [Fact]
    public void GetPlanStructures_devuelve_lista_vacia_cuando_no_se_pide_ningun_plan()
    {
        var estructuras = _service.GetPlanStructures([]);

        estructuras.Should().BeEmpty();
    }

    [Fact]
    public void GetActiveCalendars_devuelve_las_tres_entradas_deterministas_sin_filtrar_por_fecha()
    {
        var calendarios = _service.GetActiveCalendars(DateTime.UtcNow);
        var calendariosOtraFecha = _service.GetActiveCalendars(DateTime.UtcNow.AddYears(5));

        calendarios.Should().HaveCount(3);
        calendarios.Should().BeEquivalentTo(calendariosOtraFecha);
        calendarios.Should().Contain(c => c.PatientId == PacienteJuanPerez && c.PlanId == PlanPremiumMensual && c.Address == "Av. Las Flores #123");
        calendarios.Should().Contain(c => c.PatientId == PacienteJuanaSuarez && c.PlanId == PlanPremiumMensual);
        calendarios.Should().Contain(c => c.PatientId == PacienteJuanPerez && c.PlanId == PlanBasicMensual && c.Address == "Barrio Puno #789");
    }

    [Fact]
    public void GetActiveCalendars_no_tiene_ninguna_entrada_para_Luis_Ferrufino()
    {
        var calendarios = _service.GetActiveCalendars(DateTime.UtcNow);

        calendarios.Should().NotContain(c => c.PatientId == PacienteLuisFerrufino);
    }
}
