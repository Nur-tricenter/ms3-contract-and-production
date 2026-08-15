using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;

namespace Nurtricenter.MS3.Application;

/// <summary>
/// Registers all application-layer services (MediatR handlers, pipelines, etc.).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        services.AddSingleton<ISimulationService, SimulationDataService>();

        return services;
    }
}
