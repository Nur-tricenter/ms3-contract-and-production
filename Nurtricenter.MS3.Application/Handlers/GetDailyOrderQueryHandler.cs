using Joseco.DDD.Core.Abstractions;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISimulationService _sim;

    public GetDailyOrderQueryHandler(
        IDailyProductionOrderRepository dailyProductionOrderRepository,
        IUnitOfWork unitOfWork,
        ISimulationService sim)
    {
        _dailyProductionOrderRepository = dailyProductionOrderRepository;
        _unitOfWork = unitOfWork;
        _sim = sim;
    }

    public async Task<Result<DailyOrderResponse>> Handle(GetDailyOrderQuery request, CancellationToken cancellationToken)
    {
        var existing = await _dailyProductionOrderRepository.GetByProductionDateAsync(request.ProductionDate, cancellationToken);
        if (existing is not null)
            return MapToResponse(existing);

        // MS4: active calendars for the date (real or simulated)
        var calendars = _sim.GetActiveCalendars(request.ProductionDate);
        if (calendars.Count == 0)
            return Result.Failure<DailyOrderResponse>(
                new Error("NO_CALENDARS", $"No active calendars found for {request.ProductionDate:yyyy-MM-dd}.", ErrorType.NotFound));

        // Plan structures are only needed for calendars that do not bring MS4's menu
        var planIdsWithoutMenu = calendars
            .Where(c => c.Menu is not { Count: > 0 })
            .Select(c => c.PlanId)
            .Distinct()
            .ToList();
        var structures = planIdsWithoutMenu.Count == 0 ? [] : _sim.GetPlanStructures(planIdsWithoutMenu);

        var recipeQuantities = new Dictionary<Guid, (string Name, int Count)>();
        foreach (var calendar in calendars)
        {
            if (calendar.Menu is { Count: > 0 } menu)
            {
                foreach (var item in menu)
                    AddPortions(recipeQuantities, item.RecipeId, item.RecipeName, Math.Max(item.Rations, 1));

                continue;
            }

            var structure = structures.FirstOrDefault(s => s.CatalogPlanId == calendar.PlanId);
            if (structure is null) continue;

            foreach (var meal in structure.Meals)
                AddPortions(recipeQuantities, meal.RecipeId, _sim.GetRecipeName(meal.RecipeId) ?? $"Recipe-{meal.RecipeId.ToString()[..8]}", 1);
        }

        if (recipeQuantities.Count == 0)
            return Result.Failure<DailyOrderResponse>(
                new Error("NO_RECIPES", $"No recipes are known for the {calendars.Count} delivery(ies) of {request.ProductionDate:yyyy-MM-dd}: MS4 sent no menu and the plans have no known structure.", ErrorType.Conflict));

        var items = recipeQuantities.Select(kv =>
            new ProductionItem(kv.Key, kv.Value.Name, kv.Value.Count)).ToList();

        // Consolidate and persist the order
        var order = Core.Aggregates.DailyProductionOrder.ConsolidateOrder(request.ProductionDate, items);
        await _dailyProductionOrderRepository.AddAsync(order);
        await _unitOfWork.CommitAsync(cancellationToken);

        return MapToResponse(order);
    }

    private static void AddPortions(Dictionary<Guid, (string Name, int Count)> recipeQuantities, Guid recipeId, string recipeName, int portions)
    {
        recipeQuantities[recipeId] = recipeQuantities.TryGetValue(recipeId, out var current)
            ? (current.Name, current.Count + portions)
            : (recipeName, portions);
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
