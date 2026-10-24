namespace Nurtricenter.MS3.Infrastructure.Data.ReadModels;

public sealed class CatalogRecipe
{
    private CatalogRecipe() { }

    public CatalogRecipe(Guid recipeId, string name, string instructions)
    {
        RecipeId = recipeId;
        Apply(name, instructions);
    }

    public Guid RecipeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Instructions { get; private set; } = string.Empty;
    public DateTime UpdatedAt { get; private set; }

    public void Apply(string name, string instructions)
    {
        Name = name;
        Instructions = instructions;
        UpdatedAt = DateTime.UtcNow;
    }
}
