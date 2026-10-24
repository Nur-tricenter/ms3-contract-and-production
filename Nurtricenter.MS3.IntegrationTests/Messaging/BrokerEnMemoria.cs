using Joseco.Communication.External.Contracts.Message;
using Joseco.Communication.External.Contracts.Services;

namespace Nurtricenter.MS3.IntegrationTests.Messaging;

public sealed class BrokerEnMemoria : IExternalPublisher
{
    private readonly List<(IntegrationMessage Mensaje, string Exchange, Type TipoDeclarado)> _publicados = new();

    public bool FallarAlPublicar { get; set; }

    public IReadOnlyList<(IntegrationMessage Mensaje, string Exchange, Type TipoDeclarado)> Publicados => _publicados;

    public Task PublishAsync<T>(T message, string? destination = null, bool declareDestination = false)
        where T : IntegrationMessage
    {
        if (FallarAlPublicar)
            throw new InvalidOperationException("Broker no disponible.");

        _publicados.Add((message, destination ?? string.Empty, typeof(T)));
        return Task.CompletedTask;
    }
}
