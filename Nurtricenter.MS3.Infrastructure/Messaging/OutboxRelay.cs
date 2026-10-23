using System.Text.Json;
using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Contracting;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Production;

namespace Nurtricenter.MS3.Infrastructure.Messaging;

public sealed class OutboxRelay
{
    public const string Source = "contracting";
    public const int BatchSize = 20;

    public const string CreateScheduleEventType = "CreateSchedule";
    public const string TransferToLogisticsEventType = "TransferToLogistics";

    public const string ContractActivatedExchange = "contract-activated";
    public const string PackageValidatedExchange = "package-validated";

    public static IReadOnlyList<string> PublishedExchanges { get; } = [ContractActivatedExchange, PackageValidatedExchange];

    private static readonly JsonSerializerOptions PayloadOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ApplicationDbContext _context;
    private readonly IExternalPublisher _publisher;
    private readonly ILogger<OutboxRelay> _logger;

    public OutboxRelay(ApplicationDbContext context, IExternalPublisher publisher, ILogger<OutboxRelay> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _context.OutgoingIntegrationEvents
            .Where(e => e.Status == OutgoingIntegrationEvent.PendingStatus)
            .OrderBy(e => e.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var published = 0;

        foreach (var outgoingEvent in pending)
        {
            Task? publication;
            try
            {
                publication = Publish(outgoingEvent);
            }
            catch (JsonException exception)
            {
                _logger.LogWarning(exception, "Event {EventId} ({EventType}) has a malformed payload and will not be published.", outgoingEvent.Id, outgoingEvent.EventType);
                publication = null;
            }

            if (publication is null)
            {
                outgoingEvent.MarkAsUnroutable();
                await _context.SaveChangesAsync(cancellationToken);
                _logger.LogWarning("Event {EventId} ({EventType}) has no destination exchange; marked as {Status}.", outgoingEvent.Id, outgoingEvent.EventType, outgoingEvent.Status);
                continue;
            }

            await publication;

            outgoingEvent.MarkAsPublished(DateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
            published++;

            _logger.LogInformation("Published event {EventId} ({EventType}) to {TargetService}.", outgoingEvent.Id, outgoingEvent.EventType, outgoingEvent.TargetService);
        }

        return published;
    }

    private Task? Publish(OutgoingIntegrationEvent outgoingEvent) => outgoingEvent.EventType switch
    {
        CreateScheduleEventType => _publisher.PublishAsync(ToContractActivated(outgoingEvent), ContractActivatedExchange, declareDestination: true),
        TransferToLogisticsEventType => _publisher.PublishAsync(ToPackageValidated(outgoingEvent), PackageValidatedExchange, declareDestination: true),
        _ => null
    };

    private static ContractActivated ToContractActivated(OutgoingIntegrationEvent outgoingEvent)
    {
        var payload = ReadPayload<CreateSchedulePayload>(outgoingEvent);

        return new ContractActivated(outgoingEvent.Id.ToString(), Source)
        {
            Id = outgoingEvent.Id,
            CreatedAt = AsUtc(outgoingEvent.CreatedAt),
            ContractId = payload.ContractId,
            PatientId = payload.PatientId,
            CatalogPlanId = payload.CatalogPlanId,
            InvoiceNumber = payload.InvoiceNumber,
            TotalAmount = payload.TotalAmount
        };
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
            ProductionOrderId = payload.ProductionOrderId,
            CatalogPlanId = payload.CatalogPlanId,
            ReleasedAt = payload.ReleasedAt
        };
    }

    private static T ReadPayload<T>(OutgoingIntegrationEvent outgoingEvent)
        => JsonSerializer.Deserialize<T>(outgoingEvent.Payload, PayloadOptions)
           ?? throw new JsonException($"Event {outgoingEvent.Id} has an empty payload.");

    private static DateTimeOffset AsUtc(DateTime value)
        => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    private sealed record CreateSchedulePayload(Guid ContractId, Guid PatientId, Guid CatalogPlanId, string InvoiceNumber, decimal TotalAmount);

    private sealed record TransferToLogisticsPayload(Guid PackageId, Guid PatientId, Guid ProductionOrderId, Guid CatalogPlanId, DateTime ReleasedAt);
}
