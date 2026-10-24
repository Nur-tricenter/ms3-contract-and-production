using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Data.ReadModels;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Catalog;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Consumers;

public sealed class MealPlanPublishedConsumer : IIntegrationMessageConsumer<MealPlanPublished>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<MealPlanPublishedConsumer> _logger;

    public MealPlanPublishedConsumer(ApplicationDbContext context, ILogger<MealPlanPublishedConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(MealPlanPublished message, CancellationToken cancellationToken = default)
    {
        var plan = await _context.CatalogPlans.FirstOrDefaultAsync(p => p.PlanId == message.PlanId, cancellationToken);

        if (plan is null)
        {
            _context.CatalogPlans.Add(new CatalogPlan(message.PlanId, message.Name, message.DurationType, message.TotalDays, message.MealsPerDay));
            _logger.LogInformation("Projected catalog plan {PlanId} ({Name}).", message.PlanId, message.Name);
            return;
        }

        plan.Apply(message.Name, message.DurationType, message.TotalDays, message.MealsPerDay);
        _logger.LogInformation("Refreshed catalog plan {PlanId} ({Name}).", message.PlanId, message.Name);
    }
}
