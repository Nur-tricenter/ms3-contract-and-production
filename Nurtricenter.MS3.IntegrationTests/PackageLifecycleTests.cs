using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Core.Enums;

namespace Nurtricenter.MS3.IntegrationTests;

public sealed class PackageLifecycleTests : IClassFixture<ApiFactory>
{
    private const string RutaPaquetes = "/api/v1/production/packages";
    private const string RutaEnsamblaje = "/api/v1/production/packages/assemble";
    private const string RutaEtiquetado = "/api/v1/production/packages/label";
    private const string RutaValidacionDePaquetes = "/api/v1/production/packages/validate";
    private const string TipoEventoTransferToLogistics = "TransferToLogistics";
    private const string ServicioDestinoMS5 = "MS5";
    private const string EstadoEventoSimulado = "Simulated";
    private const string NombrePacienteJuanPerez = "Juan Perez";
    private const string NombrePacienteLuisFerrufino = "Luis Ferrufino";
    private const string DireccionPrimerCalendarioDeJuanPerez = "Av. Las Flores #123";
    private const string DireccionCalendarioDeJuanPerezParaPlanBasico = "Barrio Puno #789";
    private const string DireccionPlaceholderSinCalendario = "Address not found";
    private const string IdentificadorStaff = "STAFF-001";
    private const string IdentificadorSupervisor = "SUPERVISOR-001";

    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PacienteLuisFerrufino = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PlanBasicMensual = Guid.Parse("10000000-0000-0000-0000-000000000002");

    private readonly ApiFactory _factory;

    public PackageLifecycleTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task POST_packages_ciclo_de_vida_completo_persiste_cada_estado_y_emite_TransferToLogistics_hacia_MS5()
    {
        var client = _factory.CreateApiClient();
        var ordenDeProduccion = Guid.NewGuid();

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaPaquetes,
            new CreatePackageRequest(ordenDeProduccion, PacienteJuanPerez, PlanPremiumMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var paqueteCreado = await respuestaCreacion.Content.ReadFromJsonAsync<PackageResponse>();
        paqueteCreado.Should().NotBeNull();
        paqueteCreado!.Status.Should().Be(nameof(PackageStatus.Pending));
        paqueteCreado.PackageId.Should().NotBe(Guid.Empty);

        var paqueteId = paqueteCreado.PackageId;

        var paquetePendiente = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(paqueteId));
        paquetePendiente.Should().NotBeNull();
        paquetePendiente!.Status.Should().Be(PackageStatus.Pending);
        paquetePendiente.ProductionOrderId.Should().Be(ordenDeProduccion);
        paquetePendiente.PatientId.Should().Be(PacienteJuanPerez);
        paquetePendiente.CatalogPlanId.Should().Be(PlanPremiumMensual);
        paquetePendiente.Label.Should().BeNull();
        paquetePendiente.Validation.Should().BeNull();

        var respuestaEnsamblaje = await client.PostAsJsonAsync(
            RutaEnsamblaje,
            new RegisterAssemblyRequest(paqueteId, IdentificadorStaff));

        respuestaEnsamblaje.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var paqueteEnsamblado = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(paqueteId));
        paqueteEnsamblado.Should().NotBeNull();
        paqueteEnsamblado!.Status.Should().Be(PackageStatus.Assembled);
        paqueteEnsamblado.Label.Should().BeNull();
        paqueteEnsamblado.Validation.Should().BeNull();

        var respuestaEtiquetado = await client.PostAsJsonAsync(
            RutaEtiquetado,
            new GenerateLabelRequest(paqueteId, PacienteJuanPerez));

        respuestaEtiquetado.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var numeroDeSeguimientoEsperado = $"NTR-{paqueteId.ToString("N")[..8].ToUpper()}-{DateTime.UtcNow:yyyyMMdd}";

        var paqueteEtiquetado = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(paqueteId));
        paqueteEtiquetado.Should().NotBeNull();
        paqueteEtiquetado!.Status.Should().Be(PackageStatus.Labeled);
        paqueteEtiquetado.Validation.Should().BeNull();
        paqueteEtiquetado.Label.Should().NotBeNull();
        paqueteEtiquetado.Label!.PatientName.Should().Be(NombrePacienteJuanPerez);
        paqueteEtiquetado.Label.DeliveryAddress.Should().Be(DireccionPrimerCalendarioDeJuanPerez);
        paqueteEtiquetado.Label.TrackingNumber.Should().Be(numeroDeSeguimientoEsperado);

        var respuestaValidacion = await client.PostAsJsonAsync(
            RutaValidacionDePaquetes,
            new ApproveQualityRequest(ordenDeProduccion, IdentificadorSupervisor, true, 0));

        respuestaValidacion.StatusCode.Should().Be(HttpStatusCode.OK);

        var validacion = await respuestaValidacion.Content.ReadFromJsonAsync<ValidationResponse>();
        validacion.Should().NotBeNull();
        validacion!.ProductionOrderId.Should().Be(ordenDeProduccion);
        validacion.TotalPackages.Should().Be(1);
        validacion.ValidatedCount.Should().Be(1);
        validacion.FailedCount.Should().Be(0);
        validacion.Results.Should().ContainSingle(r => r.PackageId == paqueteId && r.Success && r.Error == null);

        var paqueteValidado = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(paqueteId));
        paqueteValidado.Should().NotBeNull();
        paqueteValidado!.Status.Should().Be(PackageStatus.Validated);
        paqueteValidado.Validation.Should().NotBeNull();
        paqueteValidado.Validation!.IsApproved.Should().BeTrue();
        paqueteValidado.Validation.SupervisorId.Should().Be(IdentificadorSupervisor);

        var eventosPersistidos = await _factory.QueryDbAsync(db => db.OutgoingIntegrationEvents
            .Where(e => e.EventType == TipoEventoTransferToLogistics
                && e.Payload.Contains(paqueteId.ToString()))
            .ToListAsync());

        eventosPersistidos.Should().ContainSingle();
        eventosPersistidos[0].TargetService.Should().Be(ServicioDestinoMS5);
        eventosPersistidos[0].Status.Should().Be(EstadoEventoSimulado);
        eventosPersistidos[0].Payload.Should().Contain(paqueteId.ToString());
        eventosPersistidos[0].Payload.Should().Contain(ordenDeProduccion.ToString());
        eventosPersistidos[0].Payload.Should().Contain(PacienteJuanPerez.ToString());

        var respuestaEventos = await client.GetAsync($"/api/v1/sim/outgoing/{TipoEventoTransferToLogistics}");

        respuestaEventos.StatusCode.Should().Be(HttpStatusCode.OK);

        var eventosExpuestos = await respuestaEventos.Content.ReadFromJsonAsync<List<EventoSalienteSimulado>>();
        eventosExpuestos.Should().NotBeNull();
        eventosExpuestos.Should().ContainSingle();
        eventosExpuestos![0].Id.Should().Be(eventosPersistidos[0].Id);
        eventosExpuestos[0].EventType.Should().Be(TipoEventoTransferToLogistics);
        eventosExpuestos[0].TargetService.Should().Be(ServicioDestinoMS5);
    }

    [Fact]
    public async Task POST_packages_label_usa_la_direccion_del_primer_plan_del_paciente_ignorando_el_plan_del_paquete()
    {
        var client = _factory.CreateApiClient();
        var ordenDeProduccion = Guid.NewGuid();

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaPaquetes,
            new CreatePackageRequest(ordenDeProduccion, PacienteJuanPerez, PlanBasicMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var paqueteCreado = await respuestaCreacion.Content.ReadFromJsonAsync<PackageResponse>();
        paqueteCreado.Should().NotBeNull();

        var paqueteId = paqueteCreado!.PackageId;

        var respuestaEnsamblaje = await client.PostAsJsonAsync(
            RutaEnsamblaje,
            new RegisterAssemblyRequest(paqueteId, IdentificadorStaff));

        respuestaEnsamblaje.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var respuestaEtiquetado = await client.PostAsJsonAsync(
            RutaEtiquetado,
            new GenerateLabelRequest(paqueteId, PacienteJuanPerez));

        respuestaEtiquetado.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var paqueteEtiquetado = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(paqueteId));
        paqueteEtiquetado.Should().NotBeNull();
        paqueteEtiquetado!.Status.Should().Be(PackageStatus.Labeled);
        paqueteEtiquetado.CatalogPlanId.Should().Be(PlanBasicMensual);
        paqueteEtiquetado.Label.Should().NotBeNull();
        paqueteEtiquetado.Label!.DeliveryAddress.Should().Be(DireccionPrimerCalendarioDeJuanPerez);
        paqueteEtiquetado.Label.DeliveryAddress.Should().NotBe(DireccionCalendarioDeJuanPerezParaPlanBasico);
    }

    [Fact]
    public async Task POST_packages_label_usa_placeholder_cuando_el_paciente_no_tiene_calendario_activo()
    {
        var client = _factory.CreateApiClient();
        var ordenDeProduccion = Guid.NewGuid();

        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaPaquetes,
            new CreatePackageRequest(ordenDeProduccion, PacienteLuisFerrufino, PlanPremiumMensual));

        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);

        var paqueteCreado = await respuestaCreacion.Content.ReadFromJsonAsync<PackageResponse>();
        paqueteCreado.Should().NotBeNull();

        var paqueteId = paqueteCreado!.PackageId;

        var respuestaEnsamblaje = await client.PostAsJsonAsync(
            RutaEnsamblaje,
            new RegisterAssemblyRequest(paqueteId, IdentificadorStaff));

        respuestaEnsamblaje.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var respuestaEtiquetado = await client.PostAsJsonAsync(
            RutaEtiquetado,
            new GenerateLabelRequest(paqueteId, PacienteLuisFerrufino));

        respuestaEtiquetado.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var paqueteEtiquetado = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(paqueteId));
        paqueteEtiquetado.Should().NotBeNull();
        paqueteEtiquetado!.Status.Should().Be(PackageStatus.Labeled);
        paqueteEtiquetado.Label.Should().NotBeNull();
        paqueteEtiquetado.Label!.PatientName.Should().Be(NombrePacienteLuisFerrufino);
        paqueteEtiquetado.Label.DeliveryAddress.Should().Be(DireccionPlaceholderSinCalendario);
    }
}
