using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;
using Microsoft.Extensions.Configuration;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Messaging.Consumers;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Clinic;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.DeliveryCalendar;

namespace Nurtricenter.MS3.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public const string MealPlanPublishedQueue = "ms3.meal-plan-published";
    public const string RecipePublishedQueue = "ms3.recipe-published";
    public const string RecipeUpdatedQueue = "ms3.recipe-updated";
    public const string ProductionOrderReadyQueue = "ms3.production-order-ready";
    public const string PatientPublishedQueue = "ms3.paciente-published";

    /// <summary>
    /// Configuration key that makes the simulation read patients and next-day deliveries from the
    /// projections of MS1's and MS4's events (the platform) instead of the in-memory sample data.
    /// </summary>
    public const string ProjectionsEnabledKey = "Projections:Enabled";

    public static IServiceCollection AddCatalogProjections(this IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(ISimulationService));
        if (descriptor is null)
            return services;

        services.Remove(descriptor);
        services.AddScoped<ISimulationService>(sp =>
        {
            var inner = CreateInner(sp, descriptor);
            var context = sp.GetRequiredService<ApplicationDbContext>();

            if (sp.GetRequiredService<IConfiguration>().GetValue<bool>(ProjectionsEnabledKey))
                inner = new ProjectedDataSimulationService(inner, context);

            return new CatalogProjectionSimulationService(inner, context);
        });

        return services;
    }

    public static IServiceCollection AddOutboxRelay(this IServiceCollection services)
    {
        services.AddScoped<OutboxRelay>();
        return services;
    }

    public static IServiceCollection AddCatalogConsumerHandlers(this IServiceCollection services)
    {
        services.AddScoped<CatalogRecipeProjector>();

        services.AddTransient<MealPlanPublishedConsumer>();
        services.AddTransient<RecipePublishedConsumer>();
        services.AddTransient<RecipeUpdatedConsumer>();
        services.AddTransient<ProductionOrderReadyConsumer>();
        services.AddTransient<PatientPublishedConsumer>();

        services.AddTransient<IdempotentConsumer<MealPlanPublished, MealPlanPublishedConsumer>>();
        services.AddTransient<IdempotentConsumer<RecipePublished, RecipePublishedConsumer>>();
        services.AddTransient<IdempotentConsumer<RecipeUpdated, RecipeUpdatedConsumer>>();
        services.AddTransient<IdempotentConsumer<ProductionOrderReady, ProductionOrderReadyConsumer>>();
        services.AddTransient<IdempotentConsumer<PatientPublished, PatientPublishedConsumer>>();

        return services;
    }

    private static ISimulationService CreateInner(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is ISimulationService instance)
            return instance;

        if (descriptor.ImplementationFactory is not null)
            return (ISimulationService)descriptor.ImplementationFactory(provider);

        return (ISimulationService)ActivatorUtilities.CreateInstance(provider, descriptor.ImplementationType!);
    }
}
