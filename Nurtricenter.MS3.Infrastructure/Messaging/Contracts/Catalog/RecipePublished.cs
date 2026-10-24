using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

public record RecipePublished : IntegrationMessage
{
    public string EventType => "Recipe.Published";

    public Guid RecipeId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Instructions { get; init; } = string.Empty;
}
