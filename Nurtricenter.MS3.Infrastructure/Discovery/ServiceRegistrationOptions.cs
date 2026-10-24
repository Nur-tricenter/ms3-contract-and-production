namespace Nurtricenter.MS3.Infrastructure.Discovery;

public sealed class ServiceRegistrationOptions
{
    public const string SectionName = "ServiceRegistration";
    public const string DefaultServiceName = "contracting";

    public string? ServiceDiscoveryAddress { get; set; }
    public string? ServiceId { get; set; }
    public string ServiceName { get; set; } = DefaultServiceName;
    public string? Host { get; set; }
    public int Port { get; set; }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(ServiceDiscoveryAddress);

    public string ResolveServiceId()
        => string.IsNullOrWhiteSpace(ServiceId) ? ServiceName.Trim() : ServiceId.Trim();
}
