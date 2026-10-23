using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Data.ReadModels;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Consumers;

public sealed class RecipePublishedConsumer : IIntegrationMessageConsumer<RecipePublished>
{
    private readonly CatalogRecipeProjector _projector;

    public RecipePublishedConsumer(CatalogRecipeProjector projector)
    {
        _projector = projector;
    }

    public Task HandleAsync(RecipePublished message, CancellationToken cancellationToken = default)
        => _projector.UpsertAsync(message.RecipeId, message.Name, message.Instructions, cancellationToken);
}

public sealed class RecipeUpdatedConsumer : IIntegrationMessageConsumer<RecipeUpdated>
{
    private readonly CatalogRecipeProjector _projector;

    public RecipeUpdatedConsumer(CatalogRecipeProjector projector)
    {
        _projector = projector;
    }

    public Task HandleAsync(RecipeUpdated message, CancellationToken cancellationToken = default)
        => _projector.UpsertAsync(message.RecipeId, message.Name, message.Instructions, cancellationToken);
}

public sealed class CatalogRecipeProjector
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<CatalogRecipeProjector> _logger;

    public CatalogRecipeProjector(ApplicationDbContext context, ILogger<CatalogRecipeProjector> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task UpsertAsync(Guid recipeId, string name, string instructions, CancellationToken cancellationToken)
    {
        var recipe = await _context.CatalogRecipes.FirstOrDefaultAsync(r => r.RecipeId == recipeId, cancellationToken);

        if (recipe is null)
        {
            _context.CatalogRecipes.Add(new CatalogRecipe(recipeId, name, instructions));
            _logger.LogInformation("Projected catalog recipe {RecipeId} ({Name}).", recipeId, name);
            return;
        }

        recipe.Apply(name, instructions);
        _logger.LogInformation("Refreshed catalog recipe {RecipeId} ({Name}).", recipeId, name);
    }
}
