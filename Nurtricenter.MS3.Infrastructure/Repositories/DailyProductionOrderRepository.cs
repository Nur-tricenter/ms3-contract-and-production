using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Infrastructure.Repositories;

public sealed class DailyProductionOrderRepository : IDailyProductionOrderRepository
{
    private readonly ApplicationDbContext _context;

    public DailyProductionOrderRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<DailyProductionOrder?> GetByIdAsync(Guid id, bool throwIfNotFound = false)
    {
        return await _context.DailyProductionOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id);
    }

    public async Task<DailyProductionOrder?> GetByProductionDateAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return await _context.DailyProductionOrders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.ProductionDate.Date == date.Date, cancellationToken);
    }

    public async Task AddAsync(DailyProductionOrder entity)
    {
        await _context.DailyProductionOrders.AddAsync(entity);
    }
}
