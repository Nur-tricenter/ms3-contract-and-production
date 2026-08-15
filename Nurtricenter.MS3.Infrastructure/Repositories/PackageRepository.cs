using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Infrastructure.Repositories;

public sealed class PackageRepository : IPackageRepository
{
    private readonly ApplicationDbContext _context;

    public PackageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Package?> GetByIdAsync(Guid id, bool throwIfNotFound = false)
    {
        return await _context.Packages
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<IReadOnlyList<Package>> GetByProductionOrderIdAsync(Guid productionOrderId, CancellationToken cancellationToken = default)
    {
        return await _context.Packages
            .Where(p => p.ProductionOrderId == productionOrderId)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(Package entity)
    {
        await _context.Packages.AddAsync(entity);
    }
}
