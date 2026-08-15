using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Aggregates;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Infrastructure.Repositories;

public sealed class ControlChargeRepository : IControlChargeRepository
{
    private readonly ApplicationDbContext _context;

    public ControlChargeRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ControlCharge?> GetByIdAsync(Guid id, bool throwIfNotFound = false)
    {
        return await _context.ControlCharges
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task AddAsync(ControlCharge entity)
    {
        await _context.ControlCharges.AddAsync(entity);
    }
}
