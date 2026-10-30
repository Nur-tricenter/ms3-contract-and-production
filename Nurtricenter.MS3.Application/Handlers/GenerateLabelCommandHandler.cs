using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Simulations;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class GenerateLabelCommandHandler : IRequestHandler<GenerateLabelCommand, Result>
{
    private readonly IPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDailyProductionOrderRepository _productionOrders;
    private readonly ISimulationService _sim;

    public GenerateLabelCommandHandler(
        IPackageRepository packageRepository,
        IUnitOfWork unitOfWork,
        IDailyProductionOrderRepository productionOrders,
        ISimulationService sim)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
        _productionOrders = productionOrders;
        _sim = sim;
    }

    public async Task<Result> Handle(GenerateLabelCommand request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(request.PackageId);
        if (package is null)
            return Result.Failure(
                new Error("NOT_FOUND", $"Package {request.PackageId} not found.", ErrorType.NotFound));

        // Simulate MS1: fetch patient data
        var patient = _sim.GetPatient(request.PatientId);
        if (patient is null)
            return Result.Failure(
                new Error("PATIENT_NOT_FOUND", $"Patient {request.PatientId} not found in MS1.", ErrorType.NotFound));

        // The address comes from MS4's delivery day on the order's production date, not always tomorrow,
        // and from the package's own plan when the patient has several calendars.
        var productionOrder = await _productionOrders.GetByIdAsync(package.ProductionOrderId, true);
        var deliveryDate = productionOrder?.ProductionDate.Date ?? DateTime.UtcNow.AddDays(1).Date;
        var calendars = _sim.GetActiveCalendars(deliveryDate)
            .Where(c => c.PatientId == request.PatientId)
            .ToList();
        var calendar = calendars.FirstOrDefault(c => c.PlanId == package.CatalogPlanId) ?? calendars.FirstOrDefault();
        var address = calendar?.Address ?? "Address not found";

        package.GenerateLabel(patient.FullName, address);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
