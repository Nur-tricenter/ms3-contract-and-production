using System.Net;
using FluentAssertions;

namespace Nurtricenter.MS3.IntegrationTests;

public sealed class HealthCheckTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthCheckTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GET_health_devuelve_200_cuando_la_base_responde()
    {
        var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GET_sim_patient_devuelve_200_para_un_paciente_simulado_conocido()
    {
        var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/api/v1/sim/patients/00000000-0000-0000-0000-000000000001");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
