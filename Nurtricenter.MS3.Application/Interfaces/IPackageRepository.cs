using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Application.Interfaces;

public interface IPackageRepository : IRepository<Package>
{
    Task<IReadOnlyList<Package>> GetByProductionOrderIdAsync(Guid productionOrderId, CancellationToken cancellationToken = default);
}
