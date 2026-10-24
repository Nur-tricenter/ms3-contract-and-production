using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Nurtricenter.MS3.Infrastructure.Discovery;

namespace Nurtricenter.MS3.Api;

internal sealed class ServerAddressesPortResolver : IServerPortResolver
{
    private readonly IServer _server;

    public ServerAddressesPortResolver(IServer server)
    {
        _server = server;
    }

    public int? ResolveBoundPort()
    {
        var addresses = _server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is null)
            return null;

        int? httpsPort = null;

        foreach (var address in addresses)
        {
            if (!Uri.TryCreate(address.Replace("+", "localhost").Replace("*", "localhost"), UriKind.Absolute, out var uri) || uri.Port <= 0)
                continue;

            if (uri.Scheme == Uri.UriSchemeHttp)
                return uri.Port;

            if (uri.Scheme == Uri.UriSchemeHttps)
                httpsPort ??= uri.Port;
        }

        return httpsPort;
    }
}
