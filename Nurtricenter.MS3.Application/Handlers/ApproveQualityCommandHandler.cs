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
    private readonly ISimulationService _sim;

    public ApproveQualityCommandHandler(
        IPackageRepository packageRepository,
        IUnitOfWork unitOfWork,
        IOutgoingEventRepository outgoingEvents,
        ISimulationService sim)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
        _outgoingEvents = outgoingEvents;
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

        var deliveryAddress = package.Label?.DeliveryAddress;
        var calendar = _sim.GetActiveCalendars(DateTime.UtcNow.AddDays(1).Date)
            .FirstOrDefault(c => c.PatientId == package.PatientId && c.Address == deliveryAddress);

        var payload = JsonSerializer.Serialize(new
        {
            packageId = package.Id,
            package.PatientId,
            package.ProductionOrderId,
            package.CatalogPlanId,
            status = "validatedAndReleased",
            releasedAt = DateTime.UtcNow,
            deliveryAddress,
            trackingNumber = package.Label?.TrackingNumber,
            latitude = calendar?.Latitude,
            longitude = calendar?.Longitude
        });

        await _outgoingEvents.AddAsync(
            new OutgoingIntegrationEvent("TransferToLogistics", "MS5", payload), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
