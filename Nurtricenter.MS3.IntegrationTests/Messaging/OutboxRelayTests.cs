using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Infrastructure.Messaging;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Contracting;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Production;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class OutboxRelayTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _baseDeDatos = new();
    private readonly BrokerEnMemoria _broker = new();

    public void Dispose() => _baseDeDatos.Dispose();

    private async Task<OutgoingIntegrationEvent> RegistrarEventoAsync(string tipo, string destino, object payload)
    {
        var evento = new OutgoingIntegrationEvent(tipo, destino, JsonSerializer.Serialize(payload));
        await using var context = _baseDeDatos.CrearContexto();
        context.OutgoingIntegrationEvents.Add(evento);
        await context.SaveChangesAsync();
        return evento;
    }

    private async Task<int> EjecutarRelayAsync()
    {
        await using var context = _baseDeDatos.CrearContexto();
        var relay = new OutboxRelay(context, _broker, NullLogger<OutboxRelay>.Instance);
        return await relay.PublishPendingAsync();
    }

    private async Task<OutgoingIntegrationEvent> LeerEventoAsync(Guid id)
    {
        await using var context = _baseDeDatos.CrearContexto();
        return (await context.OutgoingIntegrationEvents.FindAsync(id))!;
    }

    [Fact]
    public async Task Relay_publica_CreateSchedule_como_ContractActivated_en_su_exchange()
    {
        var contratoId = Guid.NewGuid();
        var pacienteId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var evento = await RegistrarEventoAsync("CreateSchedule", "MS4", new
        {
            contractId = contratoId,
            PatientId = pacienteId,
            CatalogPlanId = planId,
            InvoiceNumber = "inv-2026-0500",
            TotalAmount = 550.00m
        });

        var publicados = await EjecutarRelayAsync();

        publicados.Should().Be(1);
        _broker.Publicados.Should().ContainSingle();
        var (mensaje, exchange, tipoDeclarado) = _broker.Publicados[0];
        exchange.Should().Be("contract-activated");
        tipoDeclarado.Should().Be(typeof(ContractActivated));
        var contrato = mensaje.Should().BeOfType<ContractActivated>().Subject;
        contrato.Id.Should().Be(evento.Id);
        contrato.Source.Should().Be("contracting");
        contrato.ContractId.Should().Be(contratoId);
        contrato.PatientId.Should().Be(pacienteId);
        contrato.CatalogPlanId.Should().Be(planId);
        contrato.InvoiceNumber.Should().Be("inv-2026-0500");
        contrato.TotalAmount.Should().Be(550.00m);

        var persistido = await LeerEventoAsync(evento.Id);
        persistido.Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
        persistido.PublishedAt.Should().NotBeNull();
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
            releasedAt = liberadoEn
        });

        await EjecutarRelayAsync();

        var (mensaje, exchange, _) = _broker.Publicados.Should().ContainSingle().Subject;
        exchange.Should().Be("package-validated");
        var paquete = mensaje.Should().BeOfType<PackageValidated>().Subject;
        paquete.Id.Should().Be(evento.Id);
        paquete.PackageId.Should().Be(paqueteId);
        paquete.ProductionOrderId.Should().Be(ordenId);
        paquete.ReleasedAt.Should().Be(liberadoEn);
        (await LeerEventoAsync(evento.Id)).Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
    }

    [Fact]
    public async Task Relay_no_vuelve_a_publicar_un_evento_ya_publicado()
    {
        await RegistrarEventoAsync("CreateSchedule", "MS4", new { contractId = Guid.NewGuid(), PatientId = Guid.NewGuid(), CatalogPlanId = Guid.NewGuid(), InvoiceNumber = "inv-1", TotalAmount = 1m });

        await EjecutarRelayAsync();
        var segundaPasada = await EjecutarRelayAsync();

        segundaPasada.Should().Be(0);
        _broker.Publicados.Should().ContainSingle();
    }

    [Fact]
    public async Task Relay_marca_como_Unroutable_un_tipo_de_evento_sin_exchange()
    {
        var evento = await RegistrarEventoAsync("EventoDesconocido", "MS9", new { id = Guid.NewGuid() });

        await EjecutarRelayAsync();

        _broker.Publicados.Should().BeEmpty();
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
        var valido = await RegistrarEventoAsync("CreateSchedule", "MS4", new { contractId = Guid.NewGuid(), PatientId = Guid.NewGuid(), CatalogPlanId = Guid.NewGuid(), InvoiceNumber = "inv-2", TotalAmount = 2m });

        await EjecutarRelayAsync();

        (await LeerEventoAsync(malformado.Id)).Status.Should().Be(OutgoingIntegrationEvent.UnroutableStatus);
        (await LeerEventoAsync(valido.Id)).Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
        _broker.Publicados.Should().ContainSingle();
    }

    [Fact]
    public async Task Relay_deja_el_evento_pendiente_cuando_el_broker_falla()
    {
        var evento = await RegistrarEventoAsync("CreateSchedule", "MS4", new { contractId = Guid.NewGuid(), PatientId = Guid.NewGuid(), CatalogPlanId = Guid.NewGuid(), InvoiceNumber = "inv-3", TotalAmount = 3m });
        _broker.FallarAlPublicar = true;

        var ejecutar = () => EjecutarRelayAsync();

        await ejecutar.Should().ThrowAsync<InvalidOperationException>();
        var persistido = await LeerEventoAsync(evento.Id);
        persistido.Status.Should().Be(OutgoingIntegrationEvent.PendingStatus);
        persistido.PublishedAt.Should().BeNull();
    }
}
