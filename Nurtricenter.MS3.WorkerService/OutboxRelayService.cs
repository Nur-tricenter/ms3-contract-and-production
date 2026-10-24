using Nurtricenter.MS3.Infrastructure.Messaging;

namespace Nurtricenter.MS3.WorkerService;

public sealed class OutboxRelayService : BackgroundService
{
    public const string PollingIntervalKey = "Outbox:PollingIntervalSeconds";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayService> _logger;
    private readonly TimeSpan _interval;

    public OutboxRelayService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<OutboxRelayService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue(PollingIntervalKey, 5)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        do
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var relay = scope.ServiceProvider.GetRequiredService<OutboxRelay>();
                await relay.PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Outbox relay iteration failed; pending events stay pending and will be retried in {Interval}.", _interval);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
