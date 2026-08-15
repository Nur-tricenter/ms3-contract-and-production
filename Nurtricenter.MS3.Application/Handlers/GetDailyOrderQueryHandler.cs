using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Queries;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class GetDailyOrderQueryHandler : IRequestHandler<GetDailyOrderQuery, Result<DailyOrderResponse>>
{
    private readonly IDailyProductionOrderRepository _dailyProductionOrderRepository;
    private readonly ISimulationService _sim;

    public GetDailyOrderQueryHandler(
        IDailyProductionOrderRepository dailyProductionOrderRepository,
        ISimulationService sim)
    {
        _dailyProductionOrderRepository = dailyProductionOrderRepository;
        _sim = sim;
    }

    public async Task<Result<DailyOrderResponse>> Handle(GetDailyOrderQuery request, CancellationToken cancellationToken)
    {
        var existing = await _dailyProductionOrderRepository.GetByProductionDateAsync(request.ProductionDate, cancellationToken);
        if (existing is not null)
            return MapToResponse(existing);

        // Simulate MS4: get active calendars for the date
        var calendars = _sim.GetActiveCalendars(request.ProductionDate);
        if (calendars.Count == 0)
            return (Result<DailyOrderResponse>)Result.Failure(
                new Error("NO_CALENDARS", $"No active calendars found for {request.ProductionDate:yyyy-MM-dd}.", ErrorType.NotFound));

        // Simulate MS2: get plan structures for all plan IDs
        var planIds = calendars.Select(c => c.PlanId).Distinct().ToList();
        var structures = _sim.GetPlanStructures(planIds);

        // Build recipe summary by aggregating
        var recipeQuantities = new Dictionary<Guid, (string Name, int Count)>();
        foreach (var calendar in calendars)
        {
            var structure = structures.FirstOrDefault(s => s.CatalogPlanId == calendar.PlanId);
            if (structure is null) continue;

            foreach (var meal in structure.Meals)
            {
                if (recipeQuantities.ContainsKey(meal.RecipeId))
                    recipeQuantities[meal.RecipeId] = (recipeQuantities[meal.RecipeId].Name, recipeQuantities[meal.RecipeId].Count + 1);
                else
                    recipeQuantities[meal.RecipeId] = ($"Recipe-{meal.RecipeId.ToString()[..8]}", 1);
            }
        }

        var items = recipeQuantities.Select(kv =>
            new ProductionItem(kv.Key, kv.Value.Name, kv.Value.Count)).ToList();

        // Consolidate and persist the order
        var order = Core.Aggregates.DailyProductionOrder.ConsolidateOrder(request.ProductionDate, items);
        await _dailyProductionOrderRepository.AddAsync(order);

        return MapToResponse(order);
    }

    private static DailyOrderResponse MapToResponse(Core.Aggregates.DailyProductionOrder order)
    {
        var summaries = order.Items.Select(i =>
            new RecipeSummary(i.RecipeId, i.RecipeName, i.TotalQuantity)).ToList();

        return new DailyOrderResponse(
            order.Id,
            order.ProductionDate,
            order.Items.Sum(i => i.TotalQuantity),
            summaries);
    }
}
