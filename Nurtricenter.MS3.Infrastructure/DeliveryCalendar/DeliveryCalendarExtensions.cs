using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public static class DeliveryCalendarExtensions
{
    public static IServiceCollection AddDeliveryCalendar(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(DeliveryCalendarOptions.SectionName).Get<DeliveryCalendarOptions>() ?? new DeliveryCalendarOptions();

        if (!options.IsEnabled)
        {
            services.AddSingleton<IDeliveryCalendarClient, DisabledDeliveryCalendarClient>();
            return services;
        }

        services.Configure<DeliveryCalendarOptions>(configuration.GetSection(DeliveryCalendarOptions.SectionName));
        services.AddServiceAccountToken(configuration, options.BaseUrl!);

        services.AddHttpClient(HttpDeliveryCalendarClient.HttpClientName, client =>
                client.BaseAddress = new Uri(options.BaseUrl!))
            .AddHttpMessageHandler<ServiceAccountTokenHandler>();

        services.AddSingleton<IDeliveryCalendarClient, HttpDeliveryCalendarClient>();

        return services;
    }
}
