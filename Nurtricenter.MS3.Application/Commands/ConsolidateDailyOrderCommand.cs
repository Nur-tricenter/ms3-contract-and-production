using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Core.ValueObjects;

namespace Nurtricenter.MS3.Application.Commands;

/// <summary>
/// Consolidates a daily production order for the given date.
/// The handler queries MS4 for active calendars and MS2 for catalog plan structures
/// to build the list of ProductionItems.
/// </summary>
public sealed record ConsolidateDailyOrderCommand(
    DateTime ProductionDate,
    List<ProductionItem> Items
) : IRequest<Result<DailyProductionOrder>>;
