using Joseco.Communication.External.Contracts.Message;
using Joseco.Communication.External.Contracts.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nurtricenter.MS3.Infrastructure.Data;
using Nurtricenter.MS3.Infrastructure.Data.ReadModels;

namespace Nurtricenter.MS3.Infrastructure.Messaging.Consumers;

public sealed class IdempotentConsumer<T, TInner> : IIntegrationMessageConsumer<T>
    where T : IntegrationMessage
    where TInner : class, IIntegrationMessageConsumer<T>
{
    private readonly TInner _inner;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<IdempotentConsumer<T, TInner>> _logger;

    public IdempotentConsumer(TInner inner, ApplicationDbContext context, ILogger<IdempotentConsumer<T, TInner>> logger)
    {
        _inner = inner;
        _context = context;
        _logger = logger;
    }

    public async Task HandleAsync(T message, CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await _context.InboxMessages
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == message.Id, cancellationToken);

        if (alreadyProcessed)
        {
            _logger.LogInformation("Skipping {MessageType} {MessageId}: already in the inbox.", typeof(T).Name, message.Id);
            return;
        }

        _context.InboxMessages.Add(new InboxMessage(message.Id, typeof(T).Name, message.CorrelationId, message.CreatedAt));

        _logger.LogInformation("Handling {MessageType} {MessageId} from {Source}.", typeof(T).Name, message.Id, message.Source);

        await _inner.HandleAsync(message, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            if (!await WasClaimedConcurrentlyAsync(message.Id, cancellationToken))
                throw;

            _logger.LogInformation("{MessageType} {MessageId} was claimed by a concurrent delivery; skipping.", typeof(T).Name, message.Id);
        }
    }

    private async Task<bool> WasClaimedConcurrentlyAsync(Guid messageId, CancellationToken cancellationToken)
    {
        _context.ChangeTracker.Clear();
        return await _context.InboxMessages.AsNoTracking().AnyAsync(m => m.MessageId == messageId, cancellationToken);
    }
}
