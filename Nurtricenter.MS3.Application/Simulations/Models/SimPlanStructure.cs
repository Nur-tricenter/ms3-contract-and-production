namespace Nurtricenter.MS3.Application.Simulations.Models;

public sealed record SimPlanStructure(
    Guid CatalogPlanId,
    List<SimMealBlock> Meals
);

public sealed record SimMealBlock(
    string MealTime,
    Guid RecipeId
);
