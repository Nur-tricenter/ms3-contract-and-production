using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Infrastructure.Messaging;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Production;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class OutboxRelayTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _baseDeDatos = new();
    private readonly BrokerEnMemoria _broker = new();
    private readonly CalendarioEnMemoria _ms4 = new();

    public void Dispose() => _baseDeDatos.Dispose();

    private async Task<OutgoingIntegrationEvent> RegistrarEventoAsync(string tipo, string destino, object payload)
    {
        var evento = new OutgoingIntegrationEvent(tipo, destino, JsonSerializer.Serialize(payload));
        await using var context = _baseDeDatos.CrearContexto();
        context.OutgoingIntegrationEvents.Add(evento);
        await context.SaveChangesAsync();
        return evento;
    }

    private Task<OutgoingIntegrationEvent> RegistrarCreateScheduleAsync(Guid? contratoId = null, int? duracion = 30, string? inicio = "2026-10-25")
        => RegistrarEventoAsync("CreateSchedule", "MS4", new
        {
            contractId = contratoId ?? Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            CatalogPlanId = Guid.NewGuid(),
            InvoiceNumber = "inv-2026-0500",
            TotalAmount = 550.00m,
            durationDays = duracion,
            startDate = inicio
        });

    private async Task<int> EjecutarRelayAsync()
    {
        await using var context = _baseDeDatos.CrearContexto();
        var relay = new OutboxRelay(context, _broker, _ms4, NullLogger<OutboxRelay>.Instance);
        return await relay.PublishPendingAsync();
    }

    private async Task<OutgoingIntegrationEvent> LeerEventoAsync(Guid id)
    {
        await using var context = _baseDeDatos.CrearContexto();
        return (await context.OutgoingIntegrationEvents.FindAsync(id))!;
    }

    [Fact]
    public async Task Relay_entrega_CreateSchedule_a_MS4_como_POST_schedule()
    {
        var contratoId = Guid.NewGuid();
        var evento = await RegistrarCreateScheduleAsync(contratoId, 30, "2026-10-25");

        var entregados = await EjecutarRelayAsync();

        entregados.Should().Be(1);
        var calendario = _ms4.CalendariosCreados.Should().ContainSingle().Subject;
        calendario.ContractId.Should().Be(contratoId);
        calendario.Duration.Should().Be(30);
        calendario.StartDate.Should().Be(new DateOnly(2026, 10, 25));
        _broker.Publicados.Should().BeEmpty();
        var persistido = await LeerEventoAsync(evento.Id);
        persistido.Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
        persistido.PublishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Relay_usa_el_dia_siguiente_a_la_fila_cuando_una_fila_antigua_no_trae_fecha_de_inicio()
    {
        var evento = await RegistrarCreateScheduleAsync(inicio: null);

        await EjecutarRelayAsync();

        _ms4.CalendariosCreados.Should().ContainSingle()
            .Which.StartDate.Should().Be(DateOnly.FromDateTime(evento.CreatedAt.AddDays(1)));
    }

    [Fact]
    public async Task Relay_marca_como_Rejected_un_CreateSchedule_sin_duracion_del_plan()
    {
        var evento = await RegistrarCreateScheduleAsync(duracion: null);

        await EjecutarRelayAsync();

        _ms4.CalendariosCreados.Should().BeEmpty();
        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.RejectedStatus);
    }

    [Fact]
    public async Task Relay_marca_como_Rejected_cuando_MS4_rechaza_el_calendario()
    {
        var evento = await RegistrarCreateScheduleAsync(duracion: 7);
        _ms4.RechazarCalendario = true;

        await EjecutarRelayAsync();

        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.RejectedStatus);
    }

    [Fact]
    public async Task Relay_deja_CreateSchedule_pendiente_cuando_MS4_no_responde()
    {
        var evento = await RegistrarCreateScheduleAsync();
        _ms4.FallarAlLlamar = true;

        var ejecutar = () => EjecutarRelayAsync();

        await ejecutar.Should().ThrowAsync<HttpRequestException>();
        var persistido = await LeerEventoAsync(evento.Id);
        persistido.Status.Should().Be(OutgoingIntegrationEvent.PendingStatus);
        persistido.PublishedAt.Should().BeNull();
    }

    [Fact]
    public async Task Relay_marca_como_Unroutable_un_CreateSchedule_cuando_MS4_no_esta_configurado()
    {
        var evento = await RegistrarCreateScheduleAsync();
        _ms4.IsEnabled = false;

        await EjecutarRelayAsync();

        _ms4.CalendariosCreados.Should().BeEmpty();
        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.UnroutableStatus);
    }

    [Fact]
    public async Task Relay_publica_TransferToLogistics_como_PackageValidated_en_su_exchange()
    {
        var paqueteId = Guid.NewGuid();
        var ordenId = Guid.NewGuid();
        var liberadoEn = new DateTime(2026, 10, 23, 15, 30, 0, DateTimeKind.Utc);
        var evento = await RegistrarEventoAsync("TransferToLogistics", "MS5", new
        {
            packageId = paqueteId,
            PatientId = Guid.NewGuid(),
            ProductionOrderId = ordenId,
            CatalogPlanId = Guid.NewGuid(),
            status = "validatedAndReleased",
            releasedAt = liberadoEn,
            deliveryAddress = "Av. Las Flores #123",
            trackingNumber = "NTR-ABCD1234-20261023",
            latitude = -12.0464,
            longitude = -77.0428
        });

        await EjecutarRelayAsync();

        var (mensaje, exchange, tipoDeclarado) = _broker.Publicados.Should().ContainSingle().Subject;
        exchange.Should().Be("package-validated");
        tipoDeclarado.Should().Be(typeof(PackageValidated));
        var paquete = mensaje.Should().BeOfType<PackageValidated>().Subject;
        paquete.Id.Should().Be(evento.Id);
        paquete.Source.Should().Be("contracting");
        paquete.PackageId.Should().Be(paqueteId);
        paquete.ProductionOrderId.Should().Be(ordenId);
        paquete.ReleasedAt.Should().Be(liberadoEn);
        paquete.DeliveryAddress.Should().Be("Av. Las Flores #123");
        paquete.TrackingNumber.Should().Be("NTR-ABCD1234-20261023");
        paquete.Latitude.Should().Be(-12.0464);
        paquete.Longitude.Should().Be(-77.0428);
        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
    }

    [Fact]
    public async Task Relay_publica_PackageValidated_de_filas_antiguas_sin_direccion_ni_coordenadas()
    {
        await RegistrarEventoAsync("TransferToLogistics", "MS5", new
        {
            packageId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ProductionOrderId = Guid.NewGuid(),
            CatalogPlanId = Guid.NewGuid(),
            status = "validatedAndReleased",
            releasedAt = DateTime.UtcNow
        });

        await EjecutarRelayAsync();

        var paquete = _broker.Publicados.Should().ContainSingle().Subject.Item1.Should().BeOfType<PackageValidated>().Subject;
        paquete.DeliveryAddress.Should().BeEmpty();
        paquete.TrackingNumber.Should().BeEmpty();
        paquete.Latitude.Should().BeNull();
        paquete.Longitude.Should().BeNull();
    }

    [Fact]
    public async Task Relay_no_vuelve_a_entregar_un_evento_ya_entregado()
    {
        await RegistrarCreateScheduleAsync();

        await EjecutarRelayAsync();
        var segundaPasada = await EjecutarRelayAsync();

        segundaPasada.Should().Be(0);
        _ms4.CalendariosCreados.Should().ContainSingle();
    }

    [Fact]
    public async Task Relay_marca_como_Unroutable_un_tipo_de_evento_sin_destino()
    {
        var evento = await RegistrarEventoAsync("EventoDesconocido", "MS9", new { id = Guid.NewGuid() });

        await EjecutarRelayAsync();

        _broker.Publicados.Should().BeEmpty();
        _ms4.CalendariosCreados.Should().BeEmpty();
        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.UnroutableStatus);
    }

    [Fact]
    public async Task Relay_marca_como_Unroutable_un_payload_malformado_y_sigue_con_el_resto()
    {
        var malformado = new OutgoingIntegrationEvent("CreateSchedule", "MS4", "{\"contractId\":\"no-es-un-guid\"}");
        await using (var context = _baseDeDatos.CrearContexto())
        {
            context.OutgoingIntegrationEvents.Add(malformado);
            await context.SaveChangesAsync();
        }
        var valido = await RegistrarCreateScheduleAsync();

        await EjecutarRelayAsync();

        (await LeerEventoAsync(malformado.Id)).Status.Should().Be(OutgoingIntegrationEvent.UnroutableStatus);
        (await LeerEventoAsync(valido.Id)).Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
        _ms4.CalendariosCreados.Should().ContainSingle();
    }

    [Fact]
    public async Task Relay_deja_PackageValidated_pendiente_cuando_el_broker_falla()
    {
        var evento = await RegistrarEventoAsync("TransferToLogistics", "MS5", new
        {
            packageId = Guid.NewGuid(),
            PatientId = Guid.NewGuid(),
            ProductionOrderId = Guid.NewGuid(),
            CatalogPlanId = Guid.NewGuid(),
            releasedAt = DateTime.UtcNow
        });
        _broker.FallarAlPublicar = true;

        var ejecutar = () => EjecutarRelayAsync();

        await ejecutar.Should().ThrowAsync<InvalidOperationException>();
        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.PendingStatus);
    }
}
