using Consul;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Nurtricenter.MS3.Infrastructure.Discovery;

public sealed class ServiceRegistration : IHostedService
{
    public const string HealthCheckPath = "/health";

    public static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(10);
    public static readonly TimeSpan HealthCheckTimeout = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(30);

    private const int MaxAttempts = 3;
    private const string DefaultHost = "localhost";
    private static readonly TimeSpan RetryBaseDelay = TimeSpan.FromMilliseconds(500);

    private readonly IConsulClient _consulClient;
    private readonly ServiceRegistrationOptions _options;
    private readonly IServerPortResolver _portResolver;
    private readonly ILogger<ServiceRegistration> _logger;
    private bool _registered;

    public ServiceRegistration(
        IConsulClient consulClient,
        IOptions<ServiceRegistrationOptions> options,
        IServerPortResolver portResolver,
        ILogger<ServiceRegistration> logger)
    {
        _consulClient = consulClient;
        _options = options.Value;
        _portResolver = portResolver;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.IsEnabled)
            return;

        var host = string.IsNullOrWhiteSpace(_options.Host) ? DefaultHost : _options.Host.Trim();
        var port = _options.Port > 0 ? _options.Port : _portResolver.ResolveBoundPort() ?? 0;

        if (port <= 0)
        {
            _logger.LogWarning("Could not resolve the port to advertise for {ServiceName}; it will not register in Consul.", _options.ServiceName);
            return;
        }

        var registration = BuildRegistration(host, port);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await _consulClient.Agent.ServiceRegister(registration, cancellationToken);
                _registered = true;
                _logger.LogInformation("Registered {ServiceId} ({ServiceName}) in Consul at {Address}:{Port}.", registration.ID, registration.Name, registration.Address, registration.Port);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Attempt {Attempt} of {MaxAttempts} to register {ServiceId} in Consul failed.", attempt, MaxAttempts, registration.ID);
            }

            if (attempt < MaxAttempts)
            {
                try
                {
                    await Task.Delay(RetryBaseDelay * Math.Pow(2, attempt - 1), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        _logger.LogWarning("Could not register {ServiceId} in Consul after {MaxAttempts} attempts; gateway discovery is degraded.", registration.ID, MaxAttempts);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (!_registered)
            return;

        try
        {
            await _consulClient.Agent.ServiceDeregister(_options.ResolveServiceId(), cancellationToken);
            _registered = false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Could not deregister {ServiceId} from Consul; it will be evicted after {Eviction}.", _options.ResolveServiceId(), DeregisterCriticalServiceAfter);
        }
    }

    private AgentServiceRegistration BuildRegistration(string host, int port) => new()
    {
        ID = _options.ResolveServiceId(),
        Name = _options.ServiceName,
        Address = host,
        Port = port,
        Check = new AgentServiceCheck
        {
            HTTP = $"http://{host}:{port}{HealthCheckPath}",
            Interval = HealthCheckInterval,
            Timeout = HealthCheckTimeout,
            DeregisterCriticalServiceAfter = DeregisterCriticalServiceAfter
        }
    };
}
