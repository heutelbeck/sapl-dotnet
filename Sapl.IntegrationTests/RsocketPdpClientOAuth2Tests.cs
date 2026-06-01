using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Client.Auth;
using Sapl.Rsocket;

namespace Sapl.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RsocketPdpClientOAuth2Tests : IAsyncLifetime
{
    private INetwork _network = null!;
    private StartedMockOAuth2Server _oauth = null!;
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync()
    {
        _network = new NetworkBuilder().Build();
        _oauth = await MockOAuth2Server.StartAsync(_network);
        _node = await SaplNode.StartAsync(new SaplNodeOptions
        {
            Network = _network,
            AllowNoAuth = false,
            AllowOAuth2 = true,
            OAuth2IssuerUri = _oauth.IssuerUri,
        });
    }

    public async Task DisposeAsync()
    {
        await _node.DisposeAsync();
        await _oauth.DisposeAsync();
        await _network.DeleteAsync();
    }

    [Fact]
    public async Task DecideOnceWithClientCredentialsTokenReturnsPermit()
    {
        using var http = new HttpClient();
        var provider = new OAuth2TokenProvider(
            new OAuth2TokenProviderOptions
            {
                IssuerUrl = _oauth.HostIssuerUri,
                ClientId = "sapl-client",
                ClientSecret = "secret",
            },
            http);
        await using var client = new RsocketPdpClient(new RsocketPdpClientOptions
        {
            Host = _node.RsocketHost,
            Port = _node.RsocketPort,
            TokenProvider = provider,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }
}
