using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Core.Enums;

namespace Nurtricenter.MS3.IntegrationTests;

public sealed class ContractLifecycleTests : IClassFixture<ApiFactory>
{
    private const string RutaContratos = "/api/v1/contracts";
    private const string RutaCobroDePlan = "/api/v1/billing/charge-plan";
    private const string TipoEventoCreateSchedule = "CreateSchedule";
    private const string ServicioDestinoMS4 = "MS4";
    private const string EstadoPagoProcesado = "processed";
    private const string EstadoEventoSimulado = "Simulated";
    private const string NombrePlanPremiumMensual = "Premium Month Plan";
    private const decimal PrecioPlanPremiumMensual = 550.00m;

    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly ApiFactory _factory;

    public ContractLifecycleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task POST_contracts_ciclo_de_vida_completo_persiste_cada_estado_y_emite_CreateSchedule_hacia_MS4()
    {
        var client = _factory.CreateApiClient();
        var numeroFactura = $"FACTURA-{Guid.NewGuid():N}";

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaContratos,
            new CreateContractRequest(PacienteJuanPerez, PlanPremiumMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var contratoCreado = await respuestaCreacion.Content.ReadFromJsonAsync<ContractResponse>();
        contratoCreado.Should().NotBeNull();
        contratoCreado!.Status.Should().Be(nameof(ContractStatus.PendingPayment));
        contratoCreado.PatientId.Should().Be(PacienteJuanPerez);
        contratoCreado.CatalogPlanId.Should().Be(PlanPremiumMensual);
        contratoCreado.Id.Should().NotBe(Guid.Empty);

        var contratoId = contratoCreado.Id;

        respuestaCreacion.Headers.Location.Should().NotBeNull();
        respuestaCreacion.Headers.Location!.ToString().Should().EndWith($"{RutaContratos}/{contratoId}");

        var contratoPersistido = await _factory.QueryDbAsync(async db => await db.Contracts.FindAsync(contratoId));
        contratoPersistido.Should().NotBeNull();
        contratoPersistido!.Status.Should().Be(ContractStatus.PendingPayment);
        contratoPersistido.PatientId.Should().Be(PacienteJuanPerez);
        contratoPersistido.CatalogPlanId.Should().Be(PlanPremiumMensual);
        contratoPersistido.Invoice.Should().BeNull();

        var respuestaConsulta = await client.GetAsync($"{RutaContratos}/{contratoId}");

        respuestaConsulta.StatusCode.Should().Be(HttpStatusCode.OK);

        var contratoConsultado = await respuestaConsulta.Content.ReadFromJsonAsync<ContractResponse>();
        contratoConsultado.Should().NotBeNull();
        contratoConsultado!.Id.Should().Be(contratoId);
        contratoConsultado.Status.Should().Be(nameof(ContractStatus.PendingPayment));
        contratoConsultado.PatientId.Should().Be(PacienteJuanPerez);
        contratoConsultado.CatalogPlanId.Should().Be(PlanPremiumMensual);

        var respuestaCobro = await client.PostAsJsonAsync(
            RutaCobroDePlan,
            new ProcessPaymentRequest(contratoId, PrecioPlanPremiumMensual, numeroFactura));

        respuestaCobro.StatusCode.Should().Be(HttpStatusCode.OK);

        var pago = await respuestaCobro.Content.ReadFromJsonAsync<PaymentResponse>();
        pago.Should().NotBeNull();
        pago!.Status.Should().Be(EstadoPagoProcesado);

        var contratoActivo = await _factory.QueryDbAsync(async db => await db.Contracts.FindAsync(contratoId));
        contratoActivo.Should().NotBeNull();
        contratoActivo!.Status.Should().Be(ContractStatus.Active);
        contratoActivo.Invoice.Should().NotBeNull();
        contratoActivo.Invoice!.Id.Should().Be(pago.InvoiceId);
        contratoActivo.Invoice.InvoiceNumber.Should().Be(numeroFactura);
        contratoActivo.Invoice.TotalAmount.Should().Be(PrecioPlanPremiumMensual);
        contratoActivo.Invoice.IsPaid.Should().BeTrue();

        var eventosPersistidos = await _factory.QueryDbAsync(db => db.OutgoingIntegrationEvents
            .Where(e => e.EventType == TipoEventoCreateSchedule
                && e.Payload.Contains(contratoId.ToString()))
            .ToListAsync());

        eventosPersistidos.Should().ContainSingle();
        eventosPersistidos[0].TargetService.Should().Be(ServicioDestinoMS4);
        eventosPersistidos[0].Status.Should().Be(EstadoEventoSimulado);
        eventosPersistidos[0].Payload.Should().Contain(contratoId.ToString());
        eventosPersistidos[0].Payload.Should().Contain(numeroFactura);

        var respuestaEventos = await client.GetAsync($"/api/v1/sim/outgoing/{TipoEventoCreateSchedule}");

        respuestaEventos.StatusCode.Should().Be(HttpStatusCode.OK);

        var eventosExpuestos = await respuestaEventos.Content.ReadFromJsonAsync<List<EventoSalienteSimulado>>();
        eventosExpuestos.Should().NotBeNull();
        eventosExpuestos.Should().ContainSingle();
        eventosExpuestos![0].Id.Should().Be(eventosPersistidos[0].Id);
        eventosExpuestos[0].EventType.Should().Be(TipoEventoCreateSchedule);
        eventosExpuestos[0].TargetService.Should().Be(ServicioDestinoMS4);

        var respuestaFactura = await client.GetAsync($"/api/v1/billing/{numeroFactura}");

        respuestaFactura.StatusCode.Should().Be(HttpStatusCode.OK);

        var factura = await respuestaFactura.Content.ReadFromJsonAsync<InvoiceResponse>();
        factura.Should().NotBeNull();
        factura!.InvoiceId.Should().Be(pago.InvoiceId);
        factura.PatientId.Should().Be(PacienteJuanPerez);
        factura.PlanName.Should().Be(NombrePlanPremiumMensual);
        factura.TotalAmount.Should().Be(PrecioPlanPremiumMensual);
        factura.IsPaid.Should().BeTrue();

        var respuestaCancelacion = await client.PutAsJsonAsync(
            $"{RutaContratos}/{contratoId}/cancel",
            new CancelContractRequest("El paciente solicita la baja del servicio"));

        respuestaCancelacion.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var contratoCancelado = await _factory.QueryDbAsync(async db => await db.Contracts.FindAsync(contratoId));
        contratoCancelado.Should().NotBeNull();
        contratoCancelado!.Status.Should().Be(ContractStatus.Canceled);
        contratoCancelado.Invoice.Should().NotBeNull();
        contratoCancelado.Invoice!.IsPaid.Should().BeTrue();
        contratoCancelado.Invoice.InvoiceNumber.Should().Be(numeroFactura);

        var respuestaConsultaFinal = await client.GetAsync($"{RutaContratos}/{contratoId}");

        respuestaConsultaFinal.StatusCode.Should().Be(HttpStatusCode.OK);

        var contratoFinal = await respuestaConsultaFinal.Content.ReadFromJsonAsync<ContractResponse>();
        contratoFinal.Should().NotBeNull();
        contratoFinal!.Id.Should().Be(contratoId);
        contratoFinal.Status.Should().Be(nameof(ContractStatus.Canceled));
    }
}

internal sealed record EventoSalienteSimulado(
    Guid Id,
    string EventType,
    string TargetService,
    string Payload,
    string Status);
