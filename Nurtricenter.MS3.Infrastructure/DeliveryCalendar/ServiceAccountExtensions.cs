using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Nurtricenter.MS3.Infrastructure.DeliveryCalendar;

public static class ServiceAccountExtensions
{
    /// <summary>
    /// Registers the identity login client and the bearer handler shared by every outbound call MS3 makes as
    /// <c>ms3-service</c>. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddServiceAccountToken(
        this IServiceCollection services, IConfiguration configuration, string fallbackIdentityBaseUrl)
    {
        services.Configure<IdentityClientOptions>(configuration.GetSection(IdentityClientOptions.SectionName));

        var identityBaseUrl = configuration[$"{IdentityClientOptions.SectionName}:{nameof(IdentityClientOptions.BaseUrl)}"];

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<ServiceAccountTokenProvider>();
        services.TryAddTransient<ServiceAccountTokenHandler>();

        services.AddHttpClient(ServiceAccountTokenProvider.HttpClientName, client =>
            client.BaseAddress = new Uri(string.IsNullOrWhiteSpace(identityBaseUrl) ? fallbackIdentityBaseUrl : identityBaseUrl));

        return services;
    }
}
