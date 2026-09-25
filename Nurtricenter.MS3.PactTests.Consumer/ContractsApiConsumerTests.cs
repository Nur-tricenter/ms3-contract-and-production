using System.Net;
using FluentAssertions;
using PactNet;
using PactNet.Matchers;

namespace Nurtricenter.MS3.PactTests.Consumer;

public sealed class ContractsApiConsumerTests
{
    private const string NombreConsumidor = "Nurtricenter Contracts Client";
    private const string NombreProveedor = "Nurtricenter MS3";

    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly IPactBuilderV4 _pactBuilder;

    public ContractsApiConsumerTests()
    {
        var pact = Pact.V4(NombreConsumidor, NombreProveedor, new PactConfig
        {
            PactDir = Path.Combine("..", "..", "..", "pacts")
        });

        _pactBuilder = pact.WithHttpInteractions();
    }

    [Fact]
    public async Task POST_contracts_crea_un_contrato_para_un_paciente_y_plan_validos()
    {
        _pactBuilder
            .UponReceiving("una solicitud para crear un contrato con paciente y plan validos")
                .Given("el paciente y el plan existen")
                .WithRequest(HttpMethod.Post, "/api/v1/contracts")
                .WithJsonBody(new
                {
                    patientId = PacienteJuanPerez,
                    catalogPlanId = PlanPremiumMensual
                })
            .WillRespond()
                .WithStatus(HttpStatusCode.Created)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    id = Match.Type(Guid.NewGuid()),
                    patientId = PacienteJuanPerez,
                    catalogPlanId = PlanPremiumMensual,
                    status = "PendingPayment",
                    createdAt = Match.Type(DateTime.UtcNow)
                });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            var client = new ContractsApiClient(ctx.MockServerUri);

            var contrato = await client.CreateContractAsync(PacienteJuanPerez, PlanPremiumMensual);

            contrato.Should().NotBeNull();
            contrato!.Id.Should().NotBeEmpty();
            contrato.PatientId.Should().Be(PacienteJuanPerez);
            contrato.CatalogPlanId.Should().Be(PlanPremiumMensual);
            contrato.Status.Should().Be("PendingPayment");
        });
    }

    [Fact]
    public async Task GET_contracts_devuelve_un_contrato_existente_por_id()
    {
        var contractId = Guid.NewGuid();

        _pactBuilder
            .UponReceiving("una solicitud para consultar un contrato existente")
                .Given("existe un contrato", new Dictionary<string, string> { ["contractId"] = contractId.ToString() })
                .WithRequest(HttpMethod.Get, $"/api/v1/contracts/{contractId}")
                .WithHeader("Accept", "application/json")
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new
                {
                    id = contractId,
                    patientId = PacienteJuanPerez,
                    catalogPlanId = PlanPremiumMensual,
                    status = Match.Type("PendingPayment"),
                    createdAt = Match.Type(DateTime.UtcNow)
                });

        await _pactBuilder.VerifyAsync(async ctx =>
        {
            var client = new ContractsApiClient(ctx.MockServerUri);

            var contrato = await client.GetContractAsync(contractId);

            contrato.Should().NotBeNull();
            contrato!.Id.Should().Be(contractId);
            contrato.PatientId.Should().Be(PacienteJuanPerez);
            contrato.CatalogPlanId.Should().Be(PlanPremiumMensual);
            contrato.Status.Should().NotBeNullOrWhiteSpace();
        });
    }
}
