using PactNet.Verifier;

namespace Nurtricenter.MS3.PactTests.Provider;

public sealed class ContractsApiProviderTests : IClassFixture<PactProviderHost>
{
    private readonly PactProviderHost _host;

    public ContractsApiProviderTests(PactProviderHost host)
    {
        _host = host;
    }

    [Fact]
    public void Nurtricenter_MS3_honra_el_pacto_con_Nurtricenter_Contracts_Client()
    {
        var pactPath = Path.Combine(
            "..", "..", "..", "..",
            "Nurtricenter.MS3.PactTests.Consumer", "pacts",
            "Nurtricenter Contracts Client-Nurtricenter MS3.json");

        using var verifier = new PactVerifier("Nurtricenter MS3");

        verifier
            .WithHttpEndpoint(_host.ServerUri)
            .WithFileSource(new FileInfo(pactPath))
            .WithProviderStateUrl(new Uri(_host.ServerUri, "/provider-states"))
            .Verify();
    }
}
