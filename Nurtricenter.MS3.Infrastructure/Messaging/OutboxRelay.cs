using System.Text.Json;
using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.DeliveryCalendar;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Production;

namespace Nurtricenter.MS3.Infrastructure.Messaging;

public sealed class OutboxRelay
{
    public const string Source = "contracting";
    public const int BatchSize = 20;

    public const string CreateScheduleEventType = "CreateSchedule";
    public const string TransferToLogisticsEventType = "TransferToLogistics";

    public const string PackageValidatedExchange = "package-validated";

    public static IReadOnlyList<string> PublishedExchanges { get; } = [PackageValidatedExchange];

    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ApplicationDbContext _context;
    private readonly IExternalPublisher _publisher;
    private readonly IDeliveryCalendarClient _deliveryCalendar;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(ApplicationDbContext context, IExternalPublisher publisher, IDeliveryCalendarClient deliveryCalendar, ILogger<OutboxRelay> logger)
    {
        _context = context;
        _publisher = publisher;
        _deliveryCalendar = deliveryCalendar;
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _context.OutgoingIntegrationEvents
            .Where(e => e.Status == OutgoingIntegrationEvent.PendingStatus)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var delivered = 0;

        foreach (var outgoingEvent in pending)
        {
            try
            {
                var delivery = Deliver(outgoingEvent, cancellationToken);
                if (delivery is null)
                {
                    await MarkAsUnroutableAsync(outgoingEvent, "it has no destination", cancellationToken);
                    continue;
                }

                await delivery;
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Event {EventId} ({EventType}) has a malformed payload.", outgoingEvent.Id, outgoingEvent.EventType);
                await MarkAsUnroutableAsync(outgoingEvent, "its payload is malformed", cancellationToken);
                continue;
            }
            catch (DeliveryCalendarRejectedException exception)
            {
                outgoingEvent.MarkAsRejected();
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning(exception, "Event {EventId} ({EventType}) was rejected by {TargetService}; marked as {Status}.", outgoingEvent.Id, outgoingEvent.EventType, outgoingEvent.TargetService, outgoingEvent.Status);
                continue;
            }

            outgoingEvent.MarkAsPublished(DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            delivered++;

            _logger.LogInformation("Delivered event {EventId} ({EventType}) to {TargetService}.", outgoingEvent.Id, outgoingEvent.EventType, outgoingEvent.TargetService);
        }

        return delivered;
    }

    private async Task MarkAsUnroutableAsync(OutgoingIntegrationEvent outgoingEvent, string reason, CancellationToken cancellationToken)
    {
        outgoingEvent.MarkAsUnroutable();
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogWarning("Event {EventId} ({EventType}) marked as {Status} because {Reason}.", outgoingEvent.Id, outgoingEvent.EventType, outgoingEvent.Status, reason);
    }

    private Task? Deliver(OutgoingIntegrationEvent outgoingEvent, CancellationToken cancellationToken) => outgoingEvent.EventType switch
    {
        CreateScheduleEventType when _deliveryCalendar.IsEnabled => _deliveryCalendar.ScheduleAsync(ToScheduleRequest(outgoingEvent), cancellationToken),
        TransferToLogisticsEventType => _publisher.PublishAsync(ToPackageValidated(outgoingEvent), PackageValidatedExchange, declareDestination: true),
        _ => null
    };

    private static ScheduleDeliveriesRequest ToScheduleRequest(OutgoingIntegrationEvent outgoingEvent)
    {
        var payload = ReadPayload<CreateSchedulePayload>(outgoingEvent);

        if (payload.DurationDays is null)
            throw new DeliveryCalendarRejectedException(0, $"Event {outgoingEvent.Id} has no plan duration; MS4 needs it to build the calendar.");

        return new ScheduleDeliveriesRequest(
            payload.ContractId,
            payload.PatientId,
            payload.CatalogPlanId,
            payload.DurationDays.Value,
            payload.StartDate ?? DateOnly.FromDateTime(outgoingEvent.CreatedAt.AddDays(1)));
    }

    private static PackageValidated ToPackageValidated(OutgoingIntegrationEvent outgoingEvent)
    {
        var payload = ReadPayload<TransferToLogisticsPayload>(outgoingEvent);

        return new PackageValidated(outgoingEvent.Id.ToString(), Source)
        {
            Id = outgoingEvent.Id,
            CreatedAt = AsUtc(outgoingEvent.CreatedAt),
            PackageId = payload.PackageId,
            PatientId = payload.PatientId,
            PatientName = payload.PatientName ?? string.Empty,
            ProductionOrderId = payload.ProductionOrderId,
            CatalogPlanId = payload.CatalogPlanId,
            ReleasedAt = payload.ReleasedAt,
            // Rows staged before the field existed were produced for the day after their release.
            DeliveryDate = payload.DeliveryDate ?? DateOnly.FromDateTime(payload.ReleasedAt.AddDays(1)),
            DeliveryAddress = payload.DeliveryAddress ?? string.Empty,
            TrackingNumber = payload.TrackingNumber ?? string.Empty,
            Latitude = payload.Latitude,
            Longitude = payload.Longitude,
            DeliveryDayId = payload.DeliveryDayId,
            PatientPhone = payload.PatientPhone
        };
    }

    private static T ReadPayload<T>(OutgoingIntegrationEvent outgoingEvent)
        => JsonSerializer.Deserialize<T>(outgoingEvent.Payload, PayloadOptions)
           ?? throw new JsonException($"Event {outgoingEvent.Id} has an empty payload.");

    private static DateTimeOffset AsUtc(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record CreateSchedulePayload(Guid ContractId, Guid PatientId, Guid CatalogPlanId, int? DurationDays, DateOnly? StartDate);

    private sealed record TransferToLogisticsPayload(
        Guid PackageId,
        Guid PatientId,
        string? PatientName,
        Guid ProductionOrderId,
        Guid CatalogPlanId,
        DateTime ReleasedAt,
        DateOnly? DeliveryDate,
        string? DeliveryAddress,
        string? TrackingNumber,
        double? Latitude,
        double? Longitude,
        int? DeliveryDayId,
        string? PatientPhone = null);
}
