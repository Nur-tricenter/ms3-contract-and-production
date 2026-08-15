using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Core.Enums;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Core.Aggregates;

/// <summary>
/// Aggregate Root representing a daily production order consolidated by the kitchen.
/// It gathers all recipes to be cooked for the next day by querying MS4 (active calendars)
/// and MS2 (catalog plan structures). Each production item references a recipe and its
/// total servings to prepare.
/// </summary>
public sealed class DailyProductionOrder : AggregateRoot
{
    public DateTime ProductionDate { get; private set; }
    public ProductionStatus Status { get; private set; }
    private readonly List<ProductionItem> _items = new();
    public IReadOnlyCollection<ProductionItem> Items => _items.AsReadOnly();

    private DailyProductionOrder() { } // EF Core

    private DailyProductionOrder(DateTime productionDate, List<ProductionItem> items)
    {
        Id = Guid.NewGuid();
        ProductionDate = productionDate;
        Status = ProductionStatus.Consolidated;
        _items = items ?? new List<ProductionItem>();
    }

    /// <summary>
    /// Consolidates a daily production order from the items received from MS2/MS4.
    /// Aggregates all recipes and their required quantities for the given production date.
    /// </summary>
    public static DailyProductionOrder ConsolidateOrder(DateTime productionDate, List<ProductionItem> items)
    {
        if (productionDate == default)
            throw new ArgumentException("ProductionDate cannot be default.", nameof(productionDate));
        if (items == null || items.Count == 0)
            throw new ArgumentException("Items list cannot be null or empty.", nameof(items));

        return new DailyProductionOrder(productionDate, items);
    }

    public void StartPreparation()
    {
        if (Status != ProductionStatus.Consolidated)
            throw new InvalidOperationException($"Cannot start preparation. Order is in '{Status}' state.");

        Status = ProductionStatus.InPreparation;
    }

    public void MarkCompleted()
    {
        if (Status != ProductionStatus.InPreparation)
            throw new InvalidOperationException($"Cannot mark as completed. Order is in '{Status}' state.");

        Status = ProductionStatus.Completed;
    }
}
