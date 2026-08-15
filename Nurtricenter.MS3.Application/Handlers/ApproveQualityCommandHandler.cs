using System.Text.Json;
using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class ApproveQualityCommandHandler : IRequestHandler<ApproveQualityCommand, Result>
{
    private readonly IPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutgoingEventRepository _outgoingEvents;

    public ApproveQualityCommandHandler(
        IPackageRepository packageRepository,
        IUnitOfWork unitOfWork,
        IOutgoingEventRepository outgoingEvents)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
        _outgoingEvents = outgoingEvents;
    }

    public async Task<Result> Handle(ApproveQualityCommand request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(request.PackageId);
        if (package is null)
            return Result.Failure(
                new Error("NOT_FOUND", $"Package {request.PackageId} not found.", ErrorType.NotFound));

        package.ApproveQualityControl(request.SupervisorId);
        await _unitOfWork.CommitAsync(cancellationToken);

        // Simulate MS5: transfer validated package to logistics
        var payload = JsonSerializer.Serialize(new
        {
            packageId = package.Id,
            package.PatientId,
            package.ProductionOrderId,
            package.CatalogPlanId,
            status = "validatedAndReleased",
            releasedAt = DateTime.UtcNow
        });

        await _outgoingEvents.AddAsync(
            new OutgoingIntegrationEvent("TransferToLogistics", "MS5", payload), cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
