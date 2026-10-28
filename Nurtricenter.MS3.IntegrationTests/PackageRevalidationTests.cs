using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Core.Enums;

namespace Nurtricenter.MS3.IntegrationTests;

/// <summary>
/// Clase propia (y por lo tanto su propia BD en memoria): cuenta eventos TransferToLogistics y no
/// debe mezclarse con los de PackageLifecycleTests.
/// </summary>
public sealed class PackageRevalidationTests : IClassFixture<ApiFactory>
{
    private const string RutaPaquetes = "/api/v1/production/packages";
    private const string RutaEnsamblaje = "/api/v1/production/packages/assemble";
    private const string RutaEtiquetado = "/api/v1/production/packages/label";
    private const string RutaValidacionDePaquetes = "/api/v1/production/packages/validate";
    private const string TipoEventoTransferToLogistics = "TransferToLogistics";
    private const string IdentificadorStaff = "STAFF-001";
    private const string IdentificadorSupervisor = "SUPERVISOR-001";

    private static readonly Guid PacienteJuanPerez = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid PacienteLuisFerrufino = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid PlanPremiumMensual = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly ApiFactory _factory;

    public PackageRevalidationTests(ApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task POST_packages_validate_de_nuevo_la_orden_solo_aprueba_los_paquetes_nuevos_y_no_duplica_TransferToLogistics()
    {
        var client = _factory.CreateApiClient();
        var ordenDeProduccion = Guid.NewGuid();

        var primerPaquete = await CrearPaqueteEtiquetadoAsync(client, ordenDeProduccion, PacienteJuanPerez);
        var primeraValidacion = await client.PostAsJsonAsync(
            RutaValidacionDePaquetes,
            new ApproveQualityRequest(ordenDeProduccion, IdentificadorSupervisor, true, 0));
        primeraValidacion.StatusCode.Should().Be(HttpStatusCode.OK);

        // Un paquete que se suma a la orden después de la primera validación.
        var segundoPaquete = await CrearPaqueteEtiquetadoAsync(client, ordenDeProduccion, PacienteLuisFerrufino);
        var segundaValidacion = await client.PostAsJsonAsync(
            RutaValidacionDePaquetes,
            new ApproveQualityRequest(ordenDeProduccion, IdentificadorSupervisor, true, 0));

        segundaValidacion.StatusCode.Should().Be(HttpStatusCode.OK);
        var validacion = await segundaValidacion.Content.ReadFromJsonAsync<ValidationResponse>();
        validacion!.TotalPackages.Should().Be(2);
        validacion.ValidatedCount.Should().Be(2);
        validacion.FailedCount.Should().Be(0);

        var segundo = await _factory.QueryDbAsync(async db => await db.Packages.FindAsync(segundoPaquete));
        segundo!.Status.Should().Be(PackageStatus.Validated);

        foreach (var paqueteId in new[] { primerPaquete, segundoPaquete })
        {
            var eventos = await _factory.QueryDbAsync(db => db.OutgoingIntegrationEvents
                .Where(e => e.EventType == TipoEventoTransferToLogistics && e.Payload.Contains(paqueteId.ToString()))
                .ToListAsync());
            eventos.Should().ContainSingle();
        }
    }

    private static async Task<Guid> CrearPaqueteEtiquetadoAsync(HttpClient client, Guid ordenDeProduccion, Guid paciente)
    {
        var respuestaCreacion = await client.PostAsJsonAsync(
            RutaPaquetes,
            new CreatePackageRequest(ordenDeProduccion, paciente, PlanPremiumMensual));
        respuestaCreacion.StatusCode.Should().Be(HttpStatusCode.Created);
        var paqueteId = (await respuestaCreacion.Content.ReadFromJsonAsync<PackageResponse>())!.PackageId;

        (await client.PostAsJsonAsync(RutaEnsamblaje, new RegisterAssemblyRequest(paqueteId, IdentificadorStaff)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.PostAsJsonAsync(RutaEtiquetado, new GenerateLabelRequest(paqueteId, paciente)))
            .StatusCode.Should().Be(HttpStatusCode.NoContent);

        return paqueteId;
    }
}
