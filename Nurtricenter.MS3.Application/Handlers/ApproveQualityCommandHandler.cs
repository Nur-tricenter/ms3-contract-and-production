using System.Text.Json;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Simulations;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class ApproveQualityCommandHandler : IRequestHandler<ApproveQualityCommand, Result>
{
    private readonly IPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutgoingEventRepository _outgoingEvents;
    private readonly IDailyProductionOrderRepository _productionOrders;
    private readonly ISimulationService _sim;

    public ApproveQualityCommandHandler(
        IPackageRepository packageRepository,
        IUnitOfWork unitOfWork,
        IOutgoingEventRepository outgoingEvents,
        IDailyProductionOrderRepository productionOrders,
        ISimulationService sim)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
        _outgoingEvents = outgoingEvents;
        _productionOrders = productionOrders;
        _sim = sim;
    }

    public async Task<Result> Handle(ApproveQualityCommand request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(request.PackageId);
        if (package is null)
            return Result.Failure(
                new Error("NOT_FOUND", $"Package {request.PackageId} not found.", ErrorType.NotFound));

        package.ApproveQualityControl(request.SupervisorId);
        await _unitOfWork.CommitAsync(cancellationToken);

        // The package is delivered on its production order's date; MS5 assigns it to that day's route.
        var productionOrder = await _productionOrders.GetByIdAsync(package.ProductionOrderId, true);
        var deliveryDate = productionOrder?.ProductionDate.Date ?? DateTime.UtcNow.AddDays(1).Date;

        var deliveryAddress = package.Label?.DeliveryAddress;
        // A patient with several contracts has one calendar (and one MS4 delivery day) per plan, often at
        // the same address: prefer the package's own plan, or the day id would point at another contract.
        var calendars = _sim.GetActiveCalendars(deliveryDate)
            .Where(c => c.PatientId == package.PatientId && c.Address == deliveryAddress)
            .ToList();
        var calendar = calendars.FirstOrDefault(c => c.PlanId == package.CatalogPlanId) ?? calendars.FirstOrDefault();

        var payload = JsonSerializer.Serialize(new
        {
            packageId = package.Id,
            package.PatientId,
            patientName = package.Label?.PatientName,
            package.ProductionOrderId,
            package.CatalogPlanId,
            status = "validatedAndReleased",
            releasedAt = DateTime.UtcNow,
            deliveryDate = DateOnly.FromDateTime(deliveryDate),
            deliveryAddress,
            trackingNumber = package.Label?.TrackingNumber,
            latitude = calendar?.Latitude,
            longitude = calendar?.Longitude,
            deliveryDayId = calendar?.DeliveryDayId
        });

        await _outgoingEvents.AddAsync(
            new OutgoingIntegrationEvent("TransferToLogistics", "MS5", payload), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
