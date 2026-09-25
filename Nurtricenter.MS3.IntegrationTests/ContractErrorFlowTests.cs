using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Dtos.Billing;
using Nurtricenter.MS3.Application.Dtos.Contracts;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Core.Enums;

namespace Nurtricenter.MS3.IntegrationTests;

public sealed class ContractErrorFlowTests : IClassFixture<ApiFactory>
{
    private const string RutaContratos = "/api/v1/contracts";
    private const string RutaCobroDePlan = "/api/v1/billing/charge-plan";
    private const string RutaCobroDeControl = "/api/v1/billing/charge-control";
    private const string RutaPaquetes = "/api/v1/production/packages";
    private const string RutaEnsamblaje = "/api/v1/production/packages/assemble";
    private const string RutaValidacionDePaquetes = "/api/v1/production/packages/validate";
    private const string TipoEventoCreateSchedule = "CreateSchedule";
    private const decimal PrecioPlanPremiumMensual = 550.00m;
    private const decimal ImporteCopago = 25.00m;

    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PacienteJuanaSuarez = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid PacienteLuisFerrufino = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly ApiFactory _factory;

    public ContractErrorFlowTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task POST_contracts_devuelve_404_cuando_el_paciente_no_existe()
    {
        var client = _factory.CreateApiClient();
        var pacienteInexistente = Guid.NewGuid();

        var respuesta = await client.PostAsJsonAsync(
            RutaContratos,
            new CreateContractRequest(pacienteInexistente, PlanPremiumMensual));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var contratoPersistido = await _factory.QueryDbAsync(async db =>
            await db.Contracts.AnyAsync(c => c.PatientId == pacienteInexistente));

        contratoPersistido.Should().BeFalse();
    }

    [Fact]
    public async Task POST_contracts_devuelve_404_cuando_el_plan_no_existe()
    {
        var client = _factory.CreateApiClient();
        var planInexistente = Guid.NewGuid();

        var respuesta = await client.PostAsJsonAsync(
            RutaContratos,
            new CreateContractRequest(PacienteJuanPerez, planInexistente));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var contratoPersistido = await _factory.QueryDbAsync(async db =>
            await db.Contracts.AnyAsync(c => c.CatalogPlanId == planInexistente));

        contratoPersistido.Should().BeFalse();
    }

    [Fact]
    public async Task GET_contracts_devuelve_404_cuando_el_contrato_no_existe()
    {
        var client = _factory.CreateApiClient();
        var contratoInexistente = Guid.NewGuid();

        var respuesta = await client.GetAsync($"{RutaContratos}/{contratoInexistente}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var contratoPersistido = await _factory.QueryDbAsync(async db =>
            await db.Contracts.FindAsync(contratoInexistente));

        contratoPersistido.Should().BeNull();
    }

    [Fact]
    public async Task POST_billing_charge_plan_devuelve_404_cuando_el_contrato_no_existe()
    {
        var client = _factory.CreateApiClient();
        var contratoInexistente = Guid.NewGuid();
        var numeroFactura = $"FACTURA-{Guid.NewGuid():N}";

        var respuesta = await client.PostAsJsonAsync(
            RutaCobroDePlan,
            new ProcessPaymentRequest(contratoInexistente, PrecioPlanPremiumMensual, numeroFactura));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var eventoEmitido = await _factory.QueryDbAsync(async db =>
            await db.OutgoingIntegrationEvents.AnyAsync(e => e.Payload.Contains(numeroFactura)));

        eventoEmitido.Should().BeFalse();
    }

    [Fact]
    public async Task GET_billing_devuelve_404_cuando_la_factura_no_existe()
    {
        var client = _factory.CreateApiClient();
        var numeroFacturaInexistente = $"FACTURA-INEXISTENTE-{Guid.NewGuid():N}";

        var respuesta = await client.GetAsync($"/api/v1/billing/{numeroFacturaInexistente}");

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var facturaPersistida = await _factory.QueryDbAsync(async db =>
            await db.Contracts.AnyAsync(c => c.Invoice != null && c.Invoice.InvoiceNumber == numeroFacturaInexistente));

        facturaPersistida.Should().BeFalse();
    }

    [Fact]
    public async Task POST_production_packages_assemble_devuelve_404_cuando_el_paquete_no_existe()
    {
        var client = _factory.CreateApiClient();
        var paqueteInexistente = Guid.NewGuid();

        var respuesta = await client.PostAsJsonAsync(
            RutaEnsamblaje,
            new RegisterAssemblyRequest(paqueteInexistente, "STAFF-001"));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var paquetePersistido = await _factory.QueryDbAsync(async db =>
            await db.Packages.FindAsync(paqueteInexistente));

        paquetePersistido.Should().BeNull();
    }

    [Fact]
    public async Task POST_production_packages_validate_devuelve_404_cuando_la_orden_no_tiene_paquetes()
    {
        var client = _factory.CreateApiClient();
        var ordenSinPaquetes = Guid.NewGuid();

        var respuesta = await client.PostAsJsonAsync(
            RutaValidacionDePaquetes,
            new ApproveQualityRequest(ordenSinPaquetes, "SUPERVISOR-001", true, 0));

        respuesta.StatusCode.Should().Be(HttpStatusCode.NotFound);

        var paquetesPersistidos = await _factory.QueryDbAsync(async db =>
            await db.Packages.CountAsync(p => p.ProductionOrderId == ordenSinPaquetes));

        paquetesPersistidos.Should().Be(0);
    }

    [Fact]
    public async Task POST_billing_charge_control_devuelve_409_cuando_el_paciente_ya_tiene_contrato_activo()
    {
        var client = _factory.CreateApiClient();
        var numeroFacturaPlan = $"FACTURA-{Guid.NewGuid():N}";
        var numeroFacturaCopago = $"COPAGO-{Guid.NewGuid():N}";

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaContratos,
            new CreateContractRequest(PacienteLuisFerrufino, PlanPremiumMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var contratoCreado = await respuestaCreacion.Content.ReadFromJsonAsync<ContractResponse>();
        contratoCreado.Should().NotBeNull();

        var respuestaPago = await client.PostAsJsonAsync(
            RutaCobroDePlan,
            new ProcessPaymentRequest(contratoCreado!.Id, PrecioPlanPremiumMensual, numeroFacturaPlan));

        respuestaPago.StatusCode.Should().Be(HttpStatusCode.OK);

        var estadoPersistido = await _factory.QueryDbAsync(async db =>
            (await db.Contracts.FindAsync(contratoCreado.Id))!.Status);

        estadoPersistido.Should().Be(ContractStatus.Active);

        var respuestaCopago = await client.PostAsJsonAsync(
            RutaCobroDeControl,
            new ProcessControlChargeRequest(PacienteLuisFerrufino, Guid.NewGuid(), ImporteCopago, numeroFacturaCopago));

        respuestaCopago.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var copagoPersistido = await _factory.QueryDbAsync(async db =>
            await db.ControlCharges.AnyAsync(c => c.InvoiceNumber == numeroFacturaCopago));

        copagoPersistido.Should().BeFalse();
    }

    [Fact]
    public async Task POST_billing_charge_plan_devuelve_500_cuando_se_paga_dos_veces_el_mismo_contrato()
    {
        var client = _factory.CreateApiClient();
        var primeraFactura = $"FACTURA-{Guid.NewGuid():N}";
        var segundaFactura = $"FACTURA-{Guid.NewGuid():N}";

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaContratos,
            new CreateContractRequest(PacienteJuanaSuarez, PlanPremiumMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var contratoCreado = await respuestaCreacion.Content.ReadFromJsonAsync<ContractResponse>();
        contratoCreado.Should().NotBeNull();

        var primerPago = await client.PostAsJsonAsync(
            RutaCobroDePlan,
            new ProcessPaymentRequest(contratoCreado!.Id, PrecioPlanPremiumMensual, primeraFactura));

        primerPago.StatusCode.Should().Be(HttpStatusCode.OK);

        var segundoPago = await client.PostAsJsonAsync(
            RutaCobroDePlan,
            new ProcessPaymentRequest(contratoCreado.Id, PrecioPlanPremiumMensual, segundaFactura));

        segundoPago.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var contratoPersistido = await _factory.QueryDbAsync(async db =>
            await db.Contracts.FindAsync(contratoCreado.Id));

        contratoPersistido.Should().NotBeNull();
        contratoPersistido!.Status.Should().Be(ContractStatus.Active);
        contratoPersistido.Invoice.Should().NotBeNull();
        contratoPersistido.Invoice!.InvoiceNumber.Should().Be(primeraFactura);

        var eventosDelSegundoPago = await _factory.QueryDbAsync(async db =>
            await db.OutgoingIntegrationEvents
                .CountAsync(e => e.EventType == TipoEventoCreateSchedule && e.Payload.Contains(segundaFactura)));

        eventosDelSegundoPago.Should().Be(0);
    }

    [Fact]
    public async Task PUT_contracts_cancel_devuelve_500_cuando_el_contrato_esta_pendiente_de_pago()
    {
        var client = _factory.CreateApiClient();

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaContratos,
            new CreateContractRequest(PacienteJuanPerez, PlanPremiumMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var contratoCreado = await respuestaCreacion.Content.ReadFromJsonAsync<ContractResponse>();
        contratoCreado.Should().NotBeNull();
        contratoCreado!.Status.Should().Be(nameof(ContractStatus.PendingPayment));

        var respuestaCancelacion = await client.PutAsJsonAsync(
            $"{RutaContratos}/{contratoCreado.Id}/cancel",
            new CancelContractRequest("El paciente solicita la baja del servicio"));

        respuestaCancelacion.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        var contratoPersistido = await _factory.QueryDbAsync(async db =>
            await db.Contracts.FindAsync(contratoCreado.Id));

        contratoPersistido.Should().NotBeNull();
        contratoPersistido!.Status.Should().Be(ContractStatus.PendingPayment);
        contratoPersistido.Invoice.Should().BeNull();
    }

    [Fact]
    public async Task POST_production_packages_validate_devuelve_400_cuando_el_batch_no_esta_confirmado()
    {
        var client = _factory.CreateApiClient();
        var ordenDeProduccion = Guid.NewGuid();

        var respuestaPaquete = await client.PostAsJsonAsync(
            RutaPaquetes,
            new CreatePackageRequest(ordenDeProduccion, PacienteJuanPerez, PlanPremiumMensual));

        respuestaPaquete.StatusCode.Should().Be(HttpStatusCode.Created);

        var paqueteCreado = await respuestaPaquete.Content.ReadFromJsonAsync<PackageResponse>();
        paqueteCreado.Should().NotBeNull();

        var respuestaValidacion = await client.PostAsJsonAsync(
            RutaValidacionDePaquetes,
            new ApproveQualityRequest(ordenDeProduccion, "SUPERVISOR-001", false, 0));

        respuestaValidacion.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var paquetePersistido = await _factory.QueryDbAsync(async db =>
            await db.Packages.FindAsync(paqueteCreado!.PackageId));

        paquetePersistido.Should().NotBeNull();
        paquetePersistido!.Status.Should().Be(PackageStatus.Pending);
        paquetePersistido.Validation.Should().BeNull();
    }

    [Fact]
    public async Task POST_production_packages_validate_devuelve_400_cuando_el_conteo_no_coincide_con_los_paquetes()
    {
        var client = _factory.CreateApiClient();
        var ordenDeProduccion = Guid.NewGuid();

        var respuestaPaquete = await client.PostAsJsonAsync(
            RutaPaquetes,
            new CreatePackageRequest(ordenDeProduccion, PacienteJuanaSuarez, PlanPremiumMensual));

        respuestaPaquete.StatusCode.Should().Be(HttpStatusCode.Created);

        var paqueteCreado = await respuestaPaquete.Content.ReadFromJsonAsync<PackageResponse>();
        paqueteCreado.Should().NotBeNull();

        var respuestaValidacion = await client.PostAsJsonAsync(
            RutaValidacionDePaquetes,
            new ApproveQualityRequest(ordenDeProduccion, "SUPERVISOR-001", true, 5));

        respuestaValidacion.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var paquetePersistido = await _factory.QueryDbAsync(async db =>
            await db.Packages.FindAsync(paqueteCreado!.PackageId));

        paquetePersistido.Should().NotBeNull();
        paquetePersistido!.Status.Should().Be(PackageStatus.Pending);
        paquetePersistido.Validation.Should().BeNull();
    }
}
