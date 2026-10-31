using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Data.ReadModels;
using Nurtricenter.MS3.Infrastructure.Messaging;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Contracting;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class CatalogProjectionFlowTests : IClassFixture<ApiFactory>
{
    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ApiFactory _factory;

    public CatalogProjectionFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> ProyectarPlanDelCatalogoAsync(string nombre)
    {
        var planId = Guid.NewGuid();
        await _factory.SeedAsync(async db =>
        {
            db.CatalogPlans.Add(new CatalogPlan(planId, nombre, "Monthly", 30, 3));
            await db.SaveChangesAsync();
        });
        return planId;
    }

    [Fact]
    public async Task POST_contracts_acepta_un_plan_que_solo_existe_en_la_proyeccion_del_catalogo()
    {
        var client = _factory.CreateApiClient();
        var planId = await ProyectarPlanDelCatalogoAsync("Plan publicado por MS2");

        var respuesta = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(PacienteJuanPerez, planId));

        respuesta.StatusCode.Should().Be(HttpStatusCode.Created);
        var contrato = await respuesta.Content.ReadFromJsonAsync<ContractResponse>();
        var persistido = await _factory.QueryDbAsync(async db => await db.Contracts.FindAsync(contrato!.Id));
        persistido!.CatalogPlanId.Should().Be(planId);
    }

    [Fact]
    public async Task POST_contracts_devuelve_404_si_el_plan_no_esta_ni_simulado_ni_proyectado()
    {
        var client = _factory.CreateApiClient();

        var respuesta = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(PacienteJuanPerez, Guid.NewGuid()));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Flow_paid_contract_and_relay_publish_ContractActivated_with_the_projected_plan_duration()
    {
        var client = _factory.CreateApiClient();
        var planId = await ProyectarPlanDelCatalogoAsync("Plan Proteico");
        var numeroFactura = $"inv-{Guid.NewGuid():N}"[..20];

        var creacion = await client.PostAsJsonAsync("/api/v1/contracts", new CreateContractRequest(PacienteJuanPerez, planId));
        var contrato = await creacion.Content.ReadFromJsonAsync<ContractResponse>();
        var cobro = await client.PostAsJsonAsync("/api/v1/billing/charge-plan", new ProcessPaymentRequest(contrato!.Id, 480.00m, numeroFactura));
        cobro.StatusCode.Should().Be(HttpStatusCode.OK);

        var broker = new BrokerEnMemoria();
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await new OutboxRelay(db, broker, NullLogger<OutboxRelay>.Instance).PublishPendingAsync();
        }

        var (mensaje, exchange, _) = broker.Publicados.Should().ContainSingle(p => p.Mensaje is ContractActivated && ((ContractActivated)p.Mensaje).ContractId == contrato.Id).Subject;
        exchange.Should().Be("contract-activated");
        var calendario = (ContractActivated)mensaje;
        calendario.CatalogPlanId.Should().Be(planId);
        calendario.PatientId.Should().Be(PacienteJuanPerez);
        calendario.DurationDays.Should().Be(30);
        calendario.StartDate.Should().Be(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

        var estado = await _factory.QueryDbAsync(db => db.OutgoingIntegrationEvents
            .Where(e => e.EventType == "CreateSchedule" && e.Payload.Contains(contrato.Id.ToString()))
            .Select(e => e.Status)
            .SingleAsync());
        estado.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
    }
}
