using Nurtricenter.MS3.Core.Common;

namespace Nurtricenter.MS3.Core.ValueObjects;

/// <summary>
/// Represents a single recipe item within a daily production order.
/// Links to a recipe from MS2 Catalog and specifies how many servings to prepare.
/// </summary>
public sealed class ProductionItem : ValueObject
{
    public Guid RecipeId { get; private set; }
    public string RecipeName { get; private set; }
    public int TotalQuantity { get; private set; }

    private ProductionItem() { } // EF Core

    public ProductionItem(Guid recipeId, string recipeName, int totalQuantity)
    {
        if (recipeId == Guid.Empty)
            throw new ArgumentException("RecipeId cannot be empty.", nameof(recipeId));
        if (string.IsNullOrWhiteSpace(recipeName))
            throw new ArgumentException("RecipeName cannot be empty.", nameof(recipeName));
        if (totalQuantity <= 0)
            throw new ArgumentException("TotalQuantity must be greater than zero.", nameof(totalQuantity));

        RecipeId = recipeId;
        RecipeName = recipeName;
        TotalQuantity = totalQuantity;
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return RecipeId;
        yield return RecipeName.ToLowerInvariant();
        yield return TotalQuantity;
    }
}
