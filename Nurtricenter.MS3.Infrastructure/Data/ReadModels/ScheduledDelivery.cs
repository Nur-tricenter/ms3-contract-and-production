namespace Nurtricenter.MS3.Infrastructure.Data.ReadModels;

/// <summary>
/// MS3's projection of one delivery MS4 announced in <c>ProductionOrderReady</c>. Not owned by MS3:
/// the daily order, the package label and the validation read it instead of calling MS4.
/// </summary>
public sealed class ScheduledDelivery
{
    private ScheduledDelivery() { }

    public ScheduledDelivery(
        int deliveryDayId, DateOnly deliveryDate, Guid patientId, Guid planId, string? hour, string address,
        double? latitude, double? longitude, string menuJson, DateTime snapshotAt)
    {
        DeliveryDayId = deliveryDayId;
        DeliveryDate = deliveryDate;
        PatientId = patientId;
        PlanId = planId;
        Hour = hour;
        Address = address;
        Latitude = latitude;
        Longitude = longitude;
        MenuJson = menuJson;
        SnapshotAt = snapshotAt;
    }

    /// <summary>MS4's delivery day id (<c>packId</c>).</summary>
    public int DeliveryDayId { get; private set; }
    public DateOnly DeliveryDate { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid PlanId { get; private set; }
    public string? Hour { get; private set; }
    public string Address { get; private set; } = string.Empty;
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }

    /// <summary>The day's menu as JSON (list of meal time, recipe and rations); read back by the sim decorator.</summary>
    public string MenuJson { get; private set; } = "[]";

    /// <summary><c>CreatedAt</c> of the snapshot this row came from, in UTC.</summary>
    public DateTime SnapshotAt { get; private set; }
}
