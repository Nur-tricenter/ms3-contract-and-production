namespace Nurtricenter.MS3.Application.Simulations.Models;

public sealed record SimActiveCalendar(
    Guid PackageId,
    Guid PatientId,
    Guid PlanId,
    string Address,
    double Latitude,
    double Longitude,
    string DeliveryTime,
    IReadOnlyList<SimMenuItem>? Menu = null
);

public sealed record SimMenuItem(
    string MealTime,
    Guid RecipeId,
    string RecipeName,
    int Rations
);
