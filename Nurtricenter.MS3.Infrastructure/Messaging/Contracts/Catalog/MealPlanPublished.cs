using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

public record MealPlanPublished : IntegrationMessage
{
    public string EventType => "MealPlan.Published";

    public Guid PlanId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DurationType { get; init; } = string.Empty;
    public int TotalDays { get; init; }
    public int MealsPerDay { get; init; }
}
