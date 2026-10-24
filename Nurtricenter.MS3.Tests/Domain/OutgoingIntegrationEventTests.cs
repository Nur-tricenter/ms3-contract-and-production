using FluentAssertions;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Tests.Domain;

public sealed class OutgoingIntegrationEventTests
{
    private static OutgoingIntegrationEvent CrearEvento()
        => new("CreateSchedule", "MS4", "{\"contractId\":\"00000000-0000-0000-0000-000000000001\"}");

    [Fact]
    public void Constructor_crea_el_evento_pendiente_y_sin_fecha_de_publicacion()
    {
        var evento = CrearEvento();

        evento.Status.Should().Be(OutgoingIntegrationEvent.PendingStatus);
        evento.IsPending.Should().BeTrue();
        evento.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsPublished_cambia_el_estado_y_registra_la_fecha()
    {
        var evento = CrearEvento();
        var publicadoEn = new DateTime(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

        evento.MarkAsPublished(publicadoEn);

        evento.Status.Should().Be(OutgoingIntegrationEvent.PublishedStatus);
        evento.IsPending.Should().BeFalse();
        evento.PublishedAt.Should().Be(publicadoEn);
    }

    [Fact]
    public void MarkAsPublished_rechaza_un_evento_ya_publicado()
    {
        var evento = CrearEvento();
        evento.MarkAsPublished(DateTime.UtcNow);

        var publicarDeNuevo = () => evento.MarkAsPublished(DateTime.UtcNow);

        publicarDeNuevo.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkAsUnroutable_cambia_el_estado_sin_fecha_de_publicacion()
    {
        var evento = CrearEvento();

        evento.MarkAsUnroutable();

        evento.Status.Should().Be(OutgoingIntegrationEvent.UnroutableStatus);
        evento.PublishedAt.Should().BeNull();
    }

    [Fact]
    public void MarkAsUnroutable_rechaza_un_evento_ya_publicado()
    {
        var evento = CrearEvento();
        evento.MarkAsPublished(DateTime.UtcNow);

        var marcar = () => evento.MarkAsUnroutable();

        marcar.Should().Throw<InvalidOperationException>();
    }
}
