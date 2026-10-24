using Joseco.Communication.External.RabbitMQ;
using Joseco.Communication.External.RabbitMQ.Services;
using Nurtricenter.MS3.Infrastructure.Messaging;
using Nurtricenter.MS3.Infrastructure.Messaging.Consumers;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

namespace Nurtricenter.MS3.WorkerService;

public static class RabbitMqExtensions
{
    public const string SectionName = "RabbitMqSettings";

    public static IServiceCollection AddContractingRabbitMq(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection(SectionName).Get<RabbitMqSettings>();

        if (settings is null || string.IsNullOrWhiteSpace(settings.Host))
            throw new InvalidOperationException($"Configuration section '{SectionName}' is missing or has no Host.");

        services.AddSingleton(settings);

        return services.AddRabbitMQ(settings);
    }

    public static IServiceCollection AddCatalogConsumers(this IServiceCollection services)
    {
        services.AddCatalogConsumerHandlers();

        services.AddRabbitMqConsumer<MealPlanPublished, IdempotentConsumer<MealPlanPublished, MealPlanPublishedConsumer>>(MessagingExtensions.MealPlanPublishedQueue);
        services.AddRabbitMqConsumer<RecipePublished, IdempotentConsumer<RecipePublished, RecipePublishedConsumer>>(MessagingExtensions.RecipePublishedQueue);
        services.AddRabbitMqConsumer<RecipeUpdated, IdempotentConsumer<RecipeUpdated, RecipeUpdatedConsumer>>(MessagingExtensions.RecipeUpdatedQueue);

        return services;
    }
}
