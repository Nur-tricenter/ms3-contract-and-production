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
    private readonly ISimulationService _sim;

    public GenerateLabelCommandHandler(
        IPackageRepository packageRepository,
        IUnitOfWork unitOfWork,
        ISimulationService sim)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
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

        // Simulate MS4: get delivery address from active calendars
        var calendars = _sim.GetActiveCalendars(DateTime.UtcNow.AddDays(1).Date);
        var calendar = calendars.FirstOrDefault(c => c.PatientId == request.PatientId);
        var address = calendar?.Address ?? "Address not found";

        package.GenerateLabel(patient.FullName, address);
        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
