namespace Nurtricenter.MS3.Infrastructure.Data.ReadModels;

public sealed class CatalogPlan
{
    private CatalogPlan() { }

    public CatalogPlan(Guid planId, string name, string durationType, int totalDays, int mealsPerDay)
    {
        PlanId = planId;
        Apply(name, durationType, totalDays, mealsPerDay);
    }

    public Guid PlanId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string DurationType { get; private set; } = string.Empty;
    public int TotalDays { get; private set; }
    public int MealsPerDay { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Apply(string name, string durationType, int totalDays, int mealsPerDay)
    {
        Name = name;
        DurationType = durationType;
        TotalDays = totalDays;
        MealsPerDay = mealsPerDay;
        UpdatedAt = DateTime.UtcNow;
    }
}
