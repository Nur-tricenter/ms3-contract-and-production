using Nurtricenter.MS3.Core.Simulations;

namespace Nurtricenter.MS3.Application.Interfaces;

public interface IOutgoingEventRepository
{
    Task AddAsync(OutgoingIntegrationEvent outgoingEvent, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutgoingIntegrationEvent>> GetByTypeAsync(string eventType, CancellationToken cancellationToken = default);
}
