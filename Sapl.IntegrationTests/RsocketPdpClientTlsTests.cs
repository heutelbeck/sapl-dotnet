using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Rsocket;

namespace Sapl.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RsocketPdpClientTlsTests : IAsyncLifetime
{
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync() => _node = await SaplNode.StartAsync(new SaplNodeOptions { Tls = true });

    public async Task DisposeAsync() => await _node.DisposeAsync();

    private RsocketPdpClient Client() => new(new RsocketPdpClientOptions
    {
        Host = _node.RsocketHost,
        Port = _node.RsocketPort,
        Tls = new RsocketTlsOptions { CaPemPath = _node.CaPemPath!, ServerName = "localhost" },
    });

    [Fact]
    public async Task DecideOnceOverRsocketTlsReturnsPermit()
    {
        await using var client = Client();

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task DecideStreamsOverRsocketTls()
    {
        await using var client = Client();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await foreach (var decision in client.Decide(ItFixtures.PermitSubscription, cts.Token))
        {
            decision.Decision.Should().Be(Decision.Permit);
            return;
        }

        Assert.Fail("The decision stream produced no decision.");
    }
}
