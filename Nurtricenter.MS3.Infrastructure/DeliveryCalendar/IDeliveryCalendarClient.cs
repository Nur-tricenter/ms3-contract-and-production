namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public interface IDeliveryCalendarClient
{
    bool IsEnabled { get; }

    Task ScheduleAsync(ScheduleDeliveriesRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveDelivery>> GetActiveTomorrowAsync(DateOnly date, CancellationToken cancellationToken = default);
}

public sealed record ScheduleDeliveriesRequest(Guid ContractId, Guid PatientId, Guid PlanId, int Duration, DateOnly StartDate);

public sealed record ActiveDelivery(int PackId, Guid PatientId, Guid PlanId, TimeOnly? Hour, string Address, double Latitude, double Longitude);

public sealed class DeliveryCalendarRejectedException : Exception
{
    public DeliveryCalendarRejectedException(int statusCode, string detail)
        : base($"MS4 rejected the schedule with HTTP {statusCode}: {detail}")
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class DisabledDeliveryCalendarClient : IDeliveryCalendarClient
{
    public bool IsEnabled => false;

    public Task ScheduleAsync(ScheduleDeliveriesRequest request, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("DeliveryCalendar:BaseUrl is not configured.");

    public Task<IReadOnlyList<ActiveDelivery>> GetActiveTomorrowAsync(DateOnly date, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("DeliveryCalendar:BaseUrl is not configured.");
}
