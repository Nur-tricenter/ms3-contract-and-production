namespace Nurtricenter.MS3.Application.Simulations.Models;

public sealed record SimPlan(
    Guid PlanId,
    string PlanName,
    decimal Cost,
    int DurationDays
);
