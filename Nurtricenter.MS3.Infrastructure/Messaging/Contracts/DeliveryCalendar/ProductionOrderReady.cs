using Joseco.Communication.External.Contracts.Message;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Contracts.DeliveryCalendar;

/// <summary>
/// Consumed from <c>ms3.production-order-ready</c>: MS4's full snapshot of one delivery date's
/// production order. Local copy of the kernel's <c>ProductionOrderReady</c> (same wire shape).
/// </summary>
public record ProductionOrderReady : IntegrationMessage
{
    public string EventType => "ProductionOrder.Ready";

    public int ProductionOrderId { get; init; }
    public DateOnly DeliveryDate { get; init; }
    public DateOnly ProductionDate { get; init; }
    public List<ProductionOrderReadyLine> Lines { get; init; } = [];
}

public record ProductionOrderReadyLine
{
    /// <summary>MS4's delivery day id (<c>packId</c>).</summary>
    public int DeliveryDayId { get; init; }
    public Guid PatientId { get; init; }
    public Guid PlanId { get; init; }

    /// <summary>Delivery hour as <c>HH:mm</c>; <c>null</c> when none was chosen.</summary>
    public string? Hour { get; init; }
    public string Address { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public List<ProductionOrderReadyMenuItem> Menu { get; init; } = [];
}

public record ProductionOrderReadyMenuItem
{
    public string MealTime { get; init; } = string.Empty;
    public int MealTimeOrder { get; init; }
    public Guid RecipeId { get; init; }
    public string RecipeName { get; init; } = string.Empty;
    public int Rations { get; init; }
}
