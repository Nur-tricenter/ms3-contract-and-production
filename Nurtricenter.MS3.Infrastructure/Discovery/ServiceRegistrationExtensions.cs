using Consul;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nurtricenter.MS3.Infrastructure.Discovery;

public static class ServiceRegistrationExtensions
{
    public static bool IsServiceDiscoveryEnabled(this IConfiguration configuration)
        => !string.IsNullOrWhiteSpace(configuration[$"{ServiceRegistrationOptions.SectionName}:{nameof(ServiceRegistrationOptions.ServiceDiscoveryAddress)}"]);

    public static IServiceCollection AddConsulServiceRegistration(this IServiceCollection services, IConfiguration configuration)
    {
        if (!configuration.IsServiceDiscoveryEnabled())
            return services;

        var address = new Uri(configuration[$"{ServiceRegistrationOptions.SectionName}:{nameof(ServiceRegistrationOptions.ServiceDiscoveryAddress)}"]!.Trim());

        services.Configure<ServiceRegistrationOptions>(configuration.GetSection(ServiceRegistrationOptions.SectionName));
        services.TryAddSingleton<IServerPortResolver, UnknownServerPortResolver>();
        services.AddSingleton<IConsulClient>(_ => new ConsulClient(cfg => cfg.Address = address));
        services.AddHostedService<ServiceRegistration>();

        return services;
    }

    private sealed class UnknownServerPortResolver : IServerPortResolver
    {
        public int? ResolveBoundPort() => null;
    }
}
