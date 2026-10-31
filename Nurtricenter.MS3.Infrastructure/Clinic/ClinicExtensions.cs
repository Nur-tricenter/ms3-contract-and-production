using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

namespace Nurtricenter.MS3.Infrastructure.Clinic;

public static class ClinicExtensions
{
    public static IServiceCollection AddClinic(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ClinicOptions.SectionName).Get<ClinicOptions>() ?? new ClinicOptions();

        if (!options.IsEnabled)
        {
            services.AddSingleton<IClinicClient, DisabledClinicClient>();
            return services;
        }

        services.Configure<ClinicOptions>(configuration.GetSection(ClinicOptions.SectionName));
        services.AddServiceAccountToken(configuration, options.BaseUrl!);

        services.AddHttpClient(HttpClinicClient.HttpClientName, client =>
                client.BaseAddress = new Uri(options.BaseUrl!))
            .AddHttpMessageHandler<ServiceAccountTokenHandler>();

        services.AddSingleton<IClinicClient, HttpClinicClient>();

        return services;
    }
}
