using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Infrastructure.Clinic;
using Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class ClinicClientTests
{
    private const string Gateway = "http://gateway:8080";
    private static readonly Guid PacienteExistente = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private sealed class PlataformaFalsa : HttpMessageHandler
    {
        public List<(string Ruta, string? Autorizacion)> Solicitudes { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Solicitudes.Add((request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString()));

            var respuesta = request.RequestUri!.AbsolutePath switch
            {
                "/api/identity-service/v1/auth/login" => Json(HttpStatusCode.OK, "{\"accessToken\":\"token-ms3-1\",\"expiresIn\":3600}"),
                var ruta when ruta == $"/api/clinica-nutricional/v1/Patient/{PacienteExistente}" =>
                    Json(HttpStatusCode.OK, $"{{\"id\":\"{PacienteExistente}\",\"ci\":\"1234567\",\"nombre\":\"Ana\",\"apellido\":\"Perez\",\"telefono\":\"+591 700 33333\",\"consultaIniciales\":[]}}"),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };

            return Task.FromResult(respuesta);
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static (IServiceProvider Proveedor, PlataformaFalsa Plataforma) Construir(bool configurado = true)
    {
        var valores = new Dictionary<string, string?>();
        if (configurado)
        {
            valores["Clinic:BaseUrl"] = Gateway;
            valores["Identity:BaseUrl"] = Gateway;
            valores["Identity:ServiceUserName"] = "ms3-service";
            valores["Identity:ServicePassword"] = "clave-de-prueba";
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var plataforma = new PlataformaFalsa();
        var services = new ServiceCollection();
        services.AddClinic(configuracion);
        services.AddHttpClient(ServiceAccountTokenProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => plataforma);
        services.AddHttpClient(HttpClinicClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => plataforma);

        return (services.BuildServiceProvider(), plataforma);
    }

    [Fact]
    public void AddClinic_sin_BaseUrl_registra_un_cliente_deshabilitado()
    {
        var (proveedor, _) = Construir(configurado: false);

        var cliente = proveedor.GetRequiredService<IClinicClient>();

        cliente.IsEnabled.Should().BeFalse();
        cliente.Should().BeOfType<DisabledClinicClient>();
    }

    [Fact]
    public async Task GetPatientAsync_lee_el_paciente_de_MS1_con_el_token_de_ms3_service()
    {
        var (proveedor, plataforma) = Construir();
        var cliente = proveedor.GetRequiredService<IClinicClient>();

        var paciente = await cliente.GetPatientAsync(PacienteExistente);

        paciente.Should().Be(new ClinicPatient(PacienteExistente, "Ana", "Perez", "+591 700 33333"));
        plataforma.Solicitudes[1].Ruta.Should().Be($"/api/clinica-nutricional/v1/Patient/{PacienteExistente}");
        plataforma.Solicitudes[1].Autorizacion.Should().Be("Bearer token-ms3-1");
    }

    [Fact]
    public async Task GetPatientAsync_devuelve_null_cuando_MS1_responde_404()
    {
        var (proveedor, _) = Construir();
        var cliente = proveedor.GetRequiredService<IClinicClient>();

        var paciente = await cliente.GetPatientAsync(Guid.NewGuid());

        paciente.Should().BeNull();
    }

    [Fact]
    public async Task ClinicSimulationService_devuelve_el_nombre_completo_y_delega_lo_demas()
    {
        var (proveedor, _) = Construir();
        var cliente = proveedor.GetRequiredService<IClinicClient>();
        var servicio = new ClinicSimulationService(new SimulationDataService(), cliente);

        var paciente = await Task.Run(() => servicio.GetPatient(PacienteExistente));
        var desconocido = await Task.Run(() => servicio.GetPatient(Guid.NewGuid()));

        paciente!.FullName.Should().Be("Ana Perez");
        paciente.PatientId.Should().Be(PacienteExistente);
        paciente.Phone.Should().Be("+591 700 33333");
        desconocido.Should().BeNull();
    }
}
