using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Application.Simulations.Models;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class DailyOrderSinRecetasTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public DailyOrderSinRecetasTests(ApiFactory factory)
    {
        _factory = factory;
    }

    private sealed class CalendarioSinMenuDePlanDesconocido : ISimulationService
    {
        public SimPatient? GetPatient(Guid patientId) => null;
        public SimPlan? GetPlan(Guid planId) => null;
        public List<SimPlanStructure> GetPlanStructures(List<Guid> planIds) => [];
        public string? GetRecipeName(Guid recipeId) => null;

        public List<SimActiveCalendar> GetActiveCalendars(DateTime date) =>
            [new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Av. Banzer 123, Santa Cruz", -17.78, -63.18, "")];
    }

    [Fact]
    public async Task GET_daily_order_devuelve_422_con_mensaje_claro_y_no_crea_la_orden_cuando_no_hay_recetas()
    {
        _factory.CreateApiClient();
        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
                services.AddScoped<ISimulationService, CalendarioSinMenuDePlanDesconocido>()))
            .CreateClient();

        var respuesta = await client.GetAsync("/api/v1/production/daily-order?productionDate=2031-01-15");

        respuesta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await respuesta.Content.ReadAsStringAsync()).Should().Contain("No recipes are known");
        var ordenes = await _factory.QueryDbAsync(db => db.DailyProductionOrders
            .CountAsync(o => o.ProductionDate == new DateTime(2031, 1, 15, 0, 0, 0, DateTimeKind.Utc)));
        ordenes.Should().Be(0);
    }
}
