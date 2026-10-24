using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

public record RecipeUpdated : IntegrationMessage
{
    public string EventType => "Recipe.Updated";

    public Guid RecipeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
}
