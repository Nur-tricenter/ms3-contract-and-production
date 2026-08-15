using Joseco.DDD.Core.Abstractions;
using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Commands;
using Nurtricenter.MS3.Application.Interfaces;

namespace Nurtricenter.MS3.Application.Handlers;

public sealed class RegisterAssemblyCommandHandler : IRequestHandler<RegisterAssemblyCommand, Result>
{
    private readonly IPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterAssemblyCommandHandler(
        IPackageRepository packageRepository,
        IUnitOfWork unitOfWork)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result> Handle(RegisterAssemblyCommand request, CancellationToken cancellationToken)
    {
        var package = await _packageRepository.GetByIdAsync(request.PackageId);

        if (package is null)
            return Result.Failure(
                new Error("NOT_FOUND", $"Package {request.PackageId} not found.", ErrorType.NotFound));

        package.RegisterAssembly(request.StaffId);

        await _unitOfWork.CommitAsync(cancellationToken);

        return Result.Success();
    }
}
