using Joseco.DDD.Core.Results;
using MediatR;
using Nurtricenter.MS3.Application.Dtos.Production;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Application.Queries;

namespace Nurtricenter.MS3.Application.Handlers;

/// <summary>
/// Lists the packages of a production order, so a caller can follow an order to its packages
/// without reading the database (cross-service gap X-17).
/// </summary>
/// <remarks>
/// Packages are looked up by <c>ProductionOrderId</c> even when the order row is missing: today
/// GET daily-order answers with an order it never commits, so packages reference ids that exist
/// nowhere else. NOT_FOUND only when there is neither an order nor a package.
/// </remarks>
public sealed class GetOrderPackagesQueryHandler
    : IRequestHandler<GetOrderPackagesQuery, Result<ProductionOrderPackagesResponse>>
{
    private readonly IDailyProductionOrderRepository _orderRepository;
    private readonly IPackageRepository _packageRepository;

    public GetOrderPackagesQueryHandler(
        IDailyProductionOrderRepository orderRepository,
        IPackageRepository packageRepository)
    {
        _orderRepository = orderRepository;
        _packageRepository = packageRepository;
    }

    public async Task<Result<ProductionOrderPackagesResponse>> Handle(
        GetOrderPackagesQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.ProductionOrderId);
        var packages = await _packageRepository.GetByProductionOrderIdAsync(request.ProductionOrderId, cancellationToken);

        if (order is null && packages.Count == 0)
            return Result.Failure<ProductionOrderPackagesResponse>(
                new Error("NOT_FOUND", $"Production order {request.ProductionOrderId} has no packages.", ErrorType.NotFound));

        var details = packages
            .Select(p => new PackageDetail(
                p.Id,
                p.PatientId,
                p.CatalogPlanId,
                p.Status.ToString(),
                p.Label?.TrackingNumber,
                p.Label?.DeliveryAddress,
                p.Validation?.ValidatedAt))
            .OrderBy(p => p.PatientId)
            .ThenBy(p => p.PackageId)
            .ToList();

        return Result<ProductionOrderPackagesResponse>.Success(new ProductionOrderPackagesResponse(
            request.ProductionOrderId, order?.ProductionDate, order?.Status.ToString(), details.Count, details));
    }
}
