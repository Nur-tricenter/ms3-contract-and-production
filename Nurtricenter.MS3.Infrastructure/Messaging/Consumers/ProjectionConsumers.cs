using System.Text.Json;
using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Data.ReadModels;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.Clinic;
using Nurtricenter.MS3.Infrastructure.Messaging.Contracts.DeliveryCalendar;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Consumers;

/// <summary>
/// <c>ms3.production-order-ready</c> → replaces the <see cref="ScheduledDelivery"/> rows of the order's
/// delivery date with the snapshot (a snapshot older than the stored rows is ignored). The daily order,
/// the labels and the validation read them instead of asking MS4.
/// </summary>
public sealed class ProductionOrderReadyConsumer : IIntegrationMessageConsumer<ProductionOrderReady>
{
    private static readonly JsonSerializerOptions MenuOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProductionOrderReadyConsumer> _logger;

    public ProductionOrderReadyConsumer(ApplicationDbContext context, ILogger<ProductionOrderReadyConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(ProductionOrderReady message, CancellationToken cancellationToken = default)
    {
        var snapshotAt = message.CreatedAt.UtcDateTime;
        var dayIds = message.Lines.Select(l => l.DeliveryDayId).ToList();

        // A day that was rescheduled sits under its old date until that date's next snapshot, so rows
        // are matched by date OR by day id: the primary key is the day id.
        var existing = await _context.ScheduledDeliveries
            .Where(d => d.DeliveryDate == message.DeliveryDate || dayIds.Contains(d.DeliveryDayId))
            .ToListAsync(cancellationToken);

        if (existing.Any(d => d.DeliveryDate == message.DeliveryDate && d.SnapshotAt > snapshotAt))
        {
            _logger.LogInformation("Ignoring production order {OrderId} for {Date}: a newer snapshot is already projected.",
                message.ProductionOrderId, message.DeliveryDate);
            return;
        }

        _context.ScheduledDeliveries.RemoveRange(existing);
        _context.ScheduledDeliveries.AddRange(message.Lines.Select(line => new ScheduledDelivery(
            line.DeliveryDayId, message.DeliveryDate, line.PatientId, line.PlanId, line.Hour, line.Address,
            line.Latitude, line.Longitude, JsonSerializer.Serialize(line.Menu, MenuOptions), snapshotAt)));

        _logger.LogInformation("Projected production order {OrderId} for {Date}: {Count} delivery(ies).",
            message.ProductionOrderId, message.DeliveryDate, message.Lines.Count);
    }

    internal static List<ProductionOrderReadyMenuItem> ReadMenu(string menuJson)
        => JsonSerializer.Deserialize<List<ProductionOrderReadyMenuItem>>(menuJson, MenuOptions) ?? [];
}

/// <summary>
/// <c>ms3.paciente-published</c> → upserts the <see cref="ClinicPatient"/> projection. A snapshot older
/// than the stored one is ignored.
/// </summary>
public sealed class PatientPublishedConsumer : IIntegrationMessageConsumer<PatientPublished>
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PatientPublishedConsumer> _logger;

    public PatientPublishedConsumer(ApplicationDbContext context, ILogger<PatientPublishedConsumer> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(PatientPublished message, CancellationToken cancellationToken = default)
    {
        var fullName = $"{message.FirstName} {message.LastName}".Trim();
        var snapshotAt = message.CreatedAt.UtcDateTime;
        var patient = await _context.ClinicPatients.FirstOrDefaultAsync(p => p.PatientId == message.PatientId, cancellationToken);

        if (patient is null)
        {
            _context.ClinicPatients.Add(new ClinicPatient(message.PatientId, fullName, message.Phone, snapshotAt));
            _logger.LogInformation("Projected clinic patient {PatientId}.", message.PatientId);
            return;
        }

        if (patient.SnapshotAt > snapshotAt)
            return;

        patient.Apply(fullName, message.Phone, snapshotAt);
        _logger.LogInformation("Refreshed clinic patient {PatientId}.", message.PatientId);
    }
}
