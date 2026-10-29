using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;
using Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class DeliveryCalendarClientTests
{
    private const string Gateway = "http://gateway:8080";

    private sealed class PlataformaFalsa : HttpMessageHandler
    {
        public List<(HttpMethod Metodo, string Ruta, string? Autorizacion, string? Cuerpo)> Solicitudes { get; } = new();

        public HttpStatusCode RespuestaSchedule { get; set; } = HttpStatusCode.OK;

        public string RespuestaActiveTomorrow { get; set; } =
            "[{\"packId\":101,\"patientId\":\"00000000-0000-0000-0000-000000000001\",\"planId\":\"10000000-0000-0000-0000-000000000001\",\"hour\":\"10:30:00\",\"address\":\"Av. Banzer 123\",\"latitude\":-17.78,\"longitude\":-63.18}]";

        public int Logins { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Solicitudes.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString(), cuerpo));

            return request.RequestUri!.AbsolutePath switch
            {
                "/api/identity-service/v1/auth/login" => Json(HttpStatusCode.OK, $"{{\"accessToken\":\"token-ms3-{++Logins}\",\"expiresIn\":3600}}"),
                "/api/delivery-calendar/v1/schedule" => Json(RespuestaSchedule, "{\"calendarId\":7,\"days\":[]}"),
                "/api/delivery-calendar/v1/calendars/active-tomorrow" => Json(HttpStatusCode.OK, RespuestaActiveTomorrow),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            };
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }

    private static (IServiceProvider Proveedor, PlataformaFalsa Plataforma) Construir(bool configurado = true)
    {
        var valores = new Dictionary<string, string?>();
        if (configurado)
        {
            valores["DeliveryCalendar:BaseUrl"] = Gateway;
            valores["Identity:BaseUrl"] = Gateway;
            valores["Identity:ServiceUserName"] = "ms3-service";
            valores["Identity:ServicePassword"] = "clave-de-prueba";
        }

        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(valores).Build();
        var plataforma = new PlataformaFalsa();
        var services = new ServiceCollection();
        services.AddDeliveryCalendar(configuracion);
        services.AddHttpClient(ServiceAccountTokenProvider.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => plataforma);
        services.AddHttpClient(HttpDeliveryCalendarClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => plataforma);

        return (services.BuildServiceProvider(), plataforma);
    }

    [Fact]
    public void AddDeliveryCalendar_sin_BaseUrl_registra_un_cliente_deshabilitado()
    {
        var (proveedor, _) = Construir(configurado: false);

        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();

        cliente.IsEnabled.Should().BeFalse();
        cliente.Should().BeOfType<DisabledDeliveryCalendarClient>();
    }

    [Fact]
    public async Task ScheduleAsync_inicia_sesion_como_ms3_service_y_envia_el_JSON_que_espera_MS4()
    {
        var (proveedor, plataforma) = Construir();
        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();
        var contratoId = Guid.NewGuid();
        var pacienteId = Guid.NewGuid();
        var planId = Guid.NewGuid();

        await cliente.ScheduleAsync(new ScheduleDeliveriesRequest(contratoId, pacienteId, planId, 30, new DateOnly(2026, 11, 1)));

        var login = plataforma.Solicitudes[0];
        login.Ruta.Should().Be("/api/identity-service/v1/auth/login");
        login.Cuerpo.Should().Contain("\"userName\":\"ms3-service\"");

        var schedule = plataforma.Solicitudes[1];
        schedule.Metodo.Should().Be(HttpMethod.Post);
        schedule.Ruta.Should().Be("/api/delivery-calendar/v1/schedule");
        schedule.Autorizacion.Should().Be("Bearer token-ms3-1");
        using var cuerpo = JsonDocument.Parse(schedule.Cuerpo!);
        cuerpo.RootElement.GetProperty("contractId").GetGuid().Should().Be(contratoId);
        cuerpo.RootElement.GetProperty("patientId").GetGuid().Should().Be(pacienteId);
        cuerpo.RootElement.GetProperty("planId").GetGuid().Should().Be(planId);
        cuerpo.RootElement.GetProperty("duration").GetInt32().Should().Be(30);
        cuerpo.RootElement.GetProperty("startDate").GetString().Should().Be("2026-11-01");
    }

    [Fact]
    public async Task ScheduleAsync_reutiliza_el_token_entre_llamadas()
    {
        var (proveedor, plataforma) = Construir();
        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();
        var solicitud = new ScheduleDeliveriesRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 15, new DateOnly(2026, 11, 1));

        await cliente.ScheduleAsync(solicitud);
        await cliente.ScheduleAsync(solicitud);

        plataforma.Logins.Should().Be(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ScheduleAsync_lanza_DeliveryCalendarRejectedException_cuando_MS4_rechaza(HttpStatusCode codigo)
    {
        var (proveedor, plataforma) = Construir();
        plataforma.RespuestaSchedule = codigo;
        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();

        var llamar = () => cliente.ScheduleAsync(new ScheduleDeliveriesRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 7, new DateOnly(2026, 11, 1)));

        (await llamar.Should().ThrowAsync<DeliveryCalendarRejectedException>()).Which.StatusCode.Should().Be((int)codigo);
    }

    [Fact]
    public async Task ScheduleAsync_lanza_HttpRequestException_cuando_MS4_falla_para_que_el_relay_reintente()
    {
        var (proveedor, plataforma) = Construir();
        plataforma.RespuestaSchedule = HttpStatusCode.ServiceUnavailable;
        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();

        var llamar = () => cliente.ScheduleAsync(new ScheduleDeliveriesRequest(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 30, new DateOnly(2026, 11, 1)));

        await llamar.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetActiveTomorrowAsync_envia_la_fecha_y_lee_las_entregas_de_MS4()
    {
        var (proveedor, plataforma) = Construir();
        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();

        var entregas = await cliente.GetActiveTomorrowAsync(new DateOnly(2026, 11, 2));

        plataforma.Solicitudes.Last().Ruta.Should().Be("/api/delivery-calendar/v1/calendars/active-tomorrow?date=2026-11-02");
        plataforma.Solicitudes.Last().Autorizacion.Should().Be("Bearer token-ms3-1");
        var entrega = entregas.Should().ContainSingle().Subject;
        entrega.PackId.Should().Be(101);
        entrega.Hour.Should().Be(new TimeOnly(10, 30));
        entrega.Address.Should().Be("Av. Banzer 123");
        entrega.Latitude.Should().Be(-17.78);
        entrega.Menu.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveTomorrowAsync_lee_el_menu_del_dia_cuando_MS4_lo_envia()
    {
        var (proveedor, plataforma) = Construir();
        plataforma.RespuestaActiveTomorrow =
            "[{\"packId\":101,\"patientId\":\"00000000-0000-0000-0000-000000000001\",\"planId\":\"10000000-0000-0000-0000-000000000001\",\"hour\":null,\"address\":\"Av. Banzer 123\",\"latitude\":-17.78,\"longitude\":-63.18," +
            "\"menu\":[{\"mealTime\":\"Cena\",\"mealTimeOrder\":2,\"recipeId\":\"20000000-0000-0000-0000-000000000009\",\"recipeName\":\"Majadito\",\"rations\":1}," +
            "{\"mealTime\":\"Almuerzo\",\"mealTimeOrder\":1,\"recipeId\":\"20000000-0000-0000-0000-000000000008\",\"recipeName\":\"Sopa de mani\",\"rations\":2}]}]";
        var cliente = proveedor.GetRequiredService<IDeliveryCalendarClient>();

        var entrega = (await cliente.GetActiveTomorrowAsync(new DateOnly(2026, 11, 2))).Should().ContainSingle().Subject;

        entrega.Menu.Should().HaveCount(2);
        entrega.Menu!.Should().Contain(m => m.RecipeName == "Sopa de mani" && m.Rations == 2 && m.MealTimeOrder == 1);
    }

    [Fact]
    public void DeliveryCalendarSimulationService_traduce_el_menu_ordenado_por_tiempo_de_comida()
    {
        var ms4 = new CalendarioEnMemoria();
        ms4.EntregasDeManana.Add(new ActiveDelivery(101, Guid.NewGuid(), Guid.NewGuid(), null, "Av. Banzer 123", -17.78, -63.18,
        [
            new ActiveDeliveryMenuItem("Cena", 2, Guid.NewGuid(), "Majadito", 1),
            new ActiveDeliveryMenuItem("Almuerzo", 1, Guid.NewGuid(), "Sopa de mani", 2)
        ]));
        var servicio = new DeliveryCalendarSimulationService(new SimulationDataService(), ms4);

        var calendario = servicio.GetActiveCalendars(new DateTime(2026, 11, 2)).Should().ContainSingle().Subject;

        calendario.Menu!.Select(m => m.RecipeName).Should().Equal("Sopa de mani", "Majadito");
        calendario.Menu![0].Rations.Should().Be(2);
        calendario.DeliveryTime.Should().BeEmpty();
    }

    [Fact]
    public void DeliveryCalendarSimulationService_traduce_active_tomorrow_al_modelo_de_calendarios_de_MS3()
    {
        var ms4 = new CalendarioEnMemoria();
        ms4.EntregasDeManana.Add(new ActiveDelivery(101, Guid.NewGuid(), Guid.NewGuid(), new TimeOnly(9, 0), "Av. Banzer 123", -17.78, -63.18));
        var servicio = new DeliveryCalendarSimulationService(new SimulationDataService(), ms4);

        var calendarios = servicio.GetActiveCalendars(new DateTime(2026, 11, 2));

        var calendario = calendarios.Should().ContainSingle().Subject;
        calendario.PackageId.Should().Be(DeliveryCalendarSimulationService.PackIdToGuid(101));
        calendario.DeliveryDayId.Should().Be(101);
        calendario.PatientId.Should().Be(ms4.EntregasDeManana[0].PatientId);
        calendario.Address.Should().Be("Av. Banzer 123");
        calendario.Latitude.Should().Be(-17.78);
        calendario.DeliveryTime.Should().Be("09:00");
    }

    [Fact]
    public void DeliveryCalendarSimulationService_delega_pacientes_y_planes_en_la_implementacion_interna()
    {
        var servicio = new DeliveryCalendarSimulationService(new SimulationDataService(), new CalendarioEnMemoria());

        servicio.GetPatient(Guid.Parse("00000000-0000-0000-0000-000000000001"))!.FullName.Should().Be("Juan Perez");
        servicio.GetPlan(Guid.Parse("10000000-0000-0000-0000-000000000001"))!.DurationDays.Should().Be(30);
        servicio.GetPlanStructures([Guid.Parse("10000000-0000-0000-0000-000000000001")]).Should().ContainSingle();
        servicio.GetRecipeName(Guid.NewGuid()).Should().BeNull();
    }
}
