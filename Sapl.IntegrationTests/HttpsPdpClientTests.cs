using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class HttpsPdpClientTests : IAsyncLifetime
{
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync() => _node = await SaplNode.StartAsync(new SaplNodeOptions { Tls = true });

    public async Task DisposeAsync() => await _node.DisposeAsync();

    [Fact]
    public async Task DecideOnceOverHttpsWithCustomCaReturnsPermit()
    {
        var client = PdpClients.Create(
            new PdpClientOptions { BaseUrl = _node.HttpUrl },
            Tls.TrustingHandler(_node.CaPemPath!));

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task DecideStreamsOverHttpsWithCustomCa()
    {
        var client = PdpClients.Create(
            new PdpClientOptions { BaseUrl = _node.HttpUrl },
            Tls.TrustingHandler(_node.CaPemPath!));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await foreach (var decision in client.Decide(ItFixtures.PermitSubscription, cts.Token))
        {
            decision.Decision.Should().Be(Decision.Permit);
            return;
        }

        Assert.Fail("The decision stream produced no decision.");
    }
}
