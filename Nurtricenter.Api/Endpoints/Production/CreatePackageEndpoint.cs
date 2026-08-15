using FastEndpoints;
using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Api.Endpoints.Production;

public sealed class CreatePackageEndpoint : Endpoint<CreatePackageRequest, PackageResponse>
{
    private readonly IPackageRepository _packageRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreatePackageEndpoint(IPackageRepository packageRepository, IUnitOfWork unitOfWork)
    {
        _packageRepository = packageRepository;
        _unitOfWork = unitOfWork;
    }

    public override void Configure()
    {
        Post("/api/v1/production/packages");
        Description(d => d
            .WithTags("Production")
            .WithSummary("Crea un nuevo paquete de producción para una orden de producción")
            .Produces<PackageResponse>(201)
            .ProducesProblem(400));
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreatePackageRequest req, CancellationToken ct)
    {
        var package = Package.Create(req.ProductionOrderId, req.PatientId, req.CatalogPlanId);
        await _packageRepository.AddAsync(package);
        await _unitOfWork.CommitAsync(ct);

        var response = new PackageResponse(package.Id, package.Status.ToString(), DateTime.UtcNow);
        await SendAsync(response, 201, ct);
    }
}
