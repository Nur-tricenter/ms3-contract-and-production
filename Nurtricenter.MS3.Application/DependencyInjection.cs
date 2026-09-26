using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Application.Simulations;

namespace Nurtricenter.MS3.Application;

/// <summary>
/// Registers all application-layer services (MediatR handlers, pipelines, etc.).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration? configuration = null)
    {
        services.AddMediatR(config =>
        {
            config.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
        });

        var mockBaseUrl = configuration?["ExternalServices:SimulationMockBaseUrl"];

        if (!string.IsNullOrWhiteSpace(mockBaseUrl))
        {
            services.AddHttpClient<ISimulationService, HttpSimulationService>(client =>
            {
                client.BaseAddress = new Uri(mockBaseUrl);
            });
        }
        else
        {
            services.AddSingleton<ISimulationService, SimulationDataService>();
        }

        return services;
    }
}
