using Microsoft.EntityFrameworkCore;
using Nurtricenter.MS3.Application.Interfaces;
using Nurtricenter.MS3.Core.Simulations;
using Nurtricenter.MS3.Infrastructure.Data;

namespace Nurtricenter.MS3.Infrastructure.Repositories;

public sealed class OutgoingEventRepository : IOutgoingEventRepository
{
    private readonly ApplicationDbContext _context;

    public OutgoingEventRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(OutgoingIntegrationEvent outgoingEvent, CancellationToken cancellationToken = default)
    {
        await _context.OutgoingIntegrationEvents.AddAsync(outgoingEvent, cancellationToken);
    }

    public async Task<IReadOnlyList<OutgoingIntegrationEvent>> GetByTypeAsync(string eventType, CancellationToken cancellationToken = default)
    {
        return await _context.OutgoingIntegrationEvents
            .Where(e => e.EventType == eventType)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
