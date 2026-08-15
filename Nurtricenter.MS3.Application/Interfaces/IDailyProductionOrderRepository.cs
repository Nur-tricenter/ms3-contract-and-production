using Joseco.DDD.Core.Abstractions;
using Nurtricenter.MS3.Core.Aggregates;

namespace Nurtricenter.MS3.Application.Interfaces;

public interface IDailyProductionOrderRepository : IRepository<DailyProductionOrder>
{
    Task<DailyProductionOrder?> GetByProductionDateAsync(DateTime date, CancellationToken cancellationToken = default);
}
