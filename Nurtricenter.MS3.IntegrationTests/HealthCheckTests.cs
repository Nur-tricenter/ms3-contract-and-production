using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.IntegrationTests;

public sealed class HealthCheckTests : IClassFixture<ApiFactory>
{
    private const string NombrePacienteJuanPerez = "Juan Perez";
    private const string EstadoClinicoActivo = "active";

    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApiFactory _factory;

    public HealthCheckTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GET_health_devuelve_200_cuando_la_api_arranca_en_memoria()
    {
        var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_sim_patient_devuelve_200_para_un_paciente_simulado_conocido()
    {
        var client = _factory.CreateApiClient();

        var response = await client.GetAsync($"/api/v1/sim/patients/{PacienteJuanPerez}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var paciente = await response.Content.ReadFromJsonAsync<SimPatient>();

        paciente.Should().NotBeNull();
        paciente!.PatientId.Should().Be(PacienteJuanPerez);
        paciente.FullName.Should().Be(NombrePacienteJuanPerez);
        paciente.ClinicalStatus.Should().Be(EstadoClinicoActivo);
    }
}
