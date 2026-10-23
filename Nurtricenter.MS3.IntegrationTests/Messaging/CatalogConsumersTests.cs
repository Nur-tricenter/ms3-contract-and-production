using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Nurtricenter.MS3.Infrastructure.Messaging.Consumers;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class CatalogConsumersTests : IDisposable
{
    private readonly BaseDeDatosEnMemoria _baseDeDatos = new();

    public void Dispose() => _baseDeDatos.Dispose();

    private async Task EntregarPlanAsync(MealPlanPublished mensaje)
    {
        await using var context = _baseDeDatos.CrearContexto();
        var consumidor = new IdempotentConsumer<MealPlanPublished, MealPlanPublishedConsumer>(
            new MealPlanPublishedConsumer(context, NullLogger<MealPlanPublishedConsumer>.Instance),
            context,
            NullLogger<IdempotentConsumer<MealPlanPublished, MealPlanPublishedConsumer>>.Instance);
        await consumidor.HandleAsync(mensaje);
    }

    private async Task EntregarRecetaPublicadaAsync(RecipePublished mensaje)
    {
        await using var context = _baseDeDatos.CrearContexto();
        var proyector = new CatalogRecipeProjector(context, NullLogger<CatalogRecipeProjector>.Instance);
        var consumidor = new IdempotentConsumer<RecipePublished, RecipePublishedConsumer>(
            new RecipePublishedConsumer(proyector),
            context,
            NullLogger<IdempotentConsumer<RecipePublished, RecipePublishedConsumer>>.Instance);
        await consumidor.HandleAsync(mensaje);
    }

    private async Task EntregarRecetaActualizadaAsync(RecipeUpdated mensaje)
    {
        await using var context = _baseDeDatos.CrearContexto();
        var proyector = new CatalogRecipeProjector(context, NullLogger<CatalogRecipeProjector>.Instance);
        var consumidor = new IdempotentConsumer<RecipeUpdated, RecipeUpdatedConsumer>(
            new RecipeUpdatedConsumer(proyector),
            context,
            NullLogger<IdempotentConsumer<RecipeUpdated, RecipeUpdatedConsumer>>.Instance);
        await consumidor.HandleAsync(mensaje);
    }

    private async Task<T> ConsultarAsync<T>(Func<Nurtricenter.MS3.Infrastructure.Data.ApplicationDbContext, Task<T>> consulta)
    {
        await using var context = _baseDeDatos.CrearContexto();
        return await consulta(context);
    }

    [Fact]
    public async Task MealPlanPublished_proyecta_el_plan_y_registra_el_mensaje_en_el_inbox()
    {
        var mensaje = new MealPlanPublished { PlanId = Guid.NewGuid(), Name = "Plan Keto", DurationType = "Monthly", TotalDays = 30, MealsPerDay = 3 };

        await EntregarPlanAsync(mensaje);

        var plan = await ConsultarAsync(db => db.CatalogPlans.SingleAsync());
        plan.PlanId.Should().Be(mensaje.PlanId);
        plan.Name.Should().Be("Plan Keto");
        plan.TotalDays.Should().Be(30);
        plan.MealsPerDay.Should().Be(3);
        (await ConsultarAsync(db => db.InboxMessages.AnyAsync(m => m.MessageId == mensaje.Id))).Should().BeTrue();
    }

    [Fact]
    public async Task MealPlanPublished_redelivery_con_el_mismo_Id_no_vuelve_a_aplicar_cambios()
    {
        var original = new MealPlanPublished { PlanId = Guid.NewGuid(), Name = "Plan Original", DurationType = "Monthly", TotalDays = 30, MealsPerDay = 3 };
        await EntregarPlanAsync(original);

        await EntregarPlanAsync(original with { Name = "Nombre que no debe aplicarse" });

        (await ConsultarAsync(db => db.CatalogPlans.SingleAsync())).Name.Should().Be("Plan Original");
        (await ConsultarAsync(db => db.InboxMessages.CountAsync())).Should().Be(1);
    }

    [Fact]
    public async Task MealPlanPublished_con_nuevo_Id_para_el_mismo_plan_actualiza_la_proyeccion()
    {
        var planId = Guid.NewGuid();
        await EntregarPlanAsync(new MealPlanPublished { PlanId = planId, Name = "Plan v1", DurationType = "Weekly", TotalDays = 7, MealsPerDay = 2 });

        await EntregarPlanAsync(new MealPlanPublished { PlanId = planId, Name = "Plan v2", DurationType = "Monthly", TotalDays = 30, MealsPerDay = 3 });

        var plan = await ConsultarAsync(db => db.CatalogPlans.SingleAsync());
        plan.Name.Should().Be("Plan v2");
        plan.TotalDays.Should().Be(30);
        (await ConsultarAsync(db => db.InboxMessages.CountAsync())).Should().Be(2);
    }

    [Fact]
    public async Task RecipePublished_y_RecipeUpdated_mantienen_una_sola_receta_actualizada()
    {
        var recetaId = Guid.NewGuid();

        await EntregarRecetaPublicadaAsync(new RecipePublished { RecipeId = recetaId, Name = "Sopa", Instructions = "Hervir" });
        await EntregarRecetaActualizadaAsync(new RecipeUpdated { RecipeId = recetaId, Name = "Sopa de quinua", Instructions = "Hervir 20 min" });

        var receta = await ConsultarAsync(db => db.CatalogRecipes.SingleAsync());
        receta.Name.Should().Be("Sopa de quinua");
        receta.Instructions.Should().Be("Hervir 20 min");
    }

    [Fact]
    public void MealPlanPublished_deserializa_el_JSON_que_publica_el_catalogo()
    {
        var planId = Guid.NewGuid();
        var mensajeId = Guid.NewGuid();
        var json = $$"""
            {"PlanId":"{{planId}}","Name":"Plan Mediterraneo","DurationType":"Monthly","TotalDays":30,"MealsPerDay":4,
             "EventType":"MealPlan.Published","Id":"{{mensajeId}}","CreatedAt":"2026-10-23T10:00:00+00:00",
             "CorrelationId":"corr-1","Source":"catalog"}
            """;

        var mensaje = JsonConvert.DeserializeObject<MealPlanPublished>(json)!;

        mensaje.Id.Should().Be(mensajeId);
        mensaje.PlanId.Should().Be(planId);
        mensaje.Name.Should().Be("Plan Mediterraneo");
        mensaje.MealsPerDay.Should().Be(4);
        mensaje.Source.Should().Be("catalog");
    }
}
