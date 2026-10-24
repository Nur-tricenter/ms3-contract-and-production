using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Messaging.Consumers;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

namespace Nurtricenter.MS3.Infrastructure.Messaging;

public static class MessagingExtensions
{
    public const string MealPlanPublishedQueue = "ms3.meal-plan-published";
    public const string RecipePublishedQueue = "ms3.recipe-published";
    public const string RecipeUpdatedQueue = "ms3.recipe-updated";

    public static IServiceCollection AddCatalogProjections(this IServiceCollection services)
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(ISimulationService));
        if (descriptor is null)
            return services;

        services.Remove(descriptor);
        services.AddScoped<ISimulationService>(sp => new CatalogProjectionSimulationService(
            CreateInner(sp, descriptor),
            sp.GetRequiredService<ApplicationDbContext>()));

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

        services.AddTransient<IdempotentConsumer<MealPlanPublished, MealPlanPublishedConsumer>>();
        services.AddTransient<IdempotentConsumer<RecipePublished, RecipePublishedConsumer>>();
        services.AddTransient<IdempotentConsumer<RecipeUpdated, RecipeUpdatedConsumer>>();

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
