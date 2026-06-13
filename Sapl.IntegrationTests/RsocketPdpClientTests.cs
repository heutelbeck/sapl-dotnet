using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Rsocket;

namespace Sapl.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class RsocketPdpClientNoAuthTests : IAsyncLifetime
{
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync() => _node = await SaplNode.StartAsync(new SaplNodeOptions());

    public async Task DisposeAsync() => await _node.DisposeAsync();

    private RsocketPdpClient Client() =>
        new(new RsocketPdpClientOptions { Host = _node.RsocketHost, Port = _node.RsocketPort });

    private static MultiAuthorizationSubscription TwoSubscriptions() => new()
    {
        Subscriptions = new Dictionary<string, AuthorizationSubscription>
        {
            ["a"] = AuthorizationSubscription.Create("alice", "read", "doc-1"),
            ["b"] = AuthorizationSubscription.Create("alice", "write", "doc-2"),
        },
    };

    [Fact]
    public async Task DecideOnceReturnsPermit()
    {
        await using var client = Client();

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task DecideStreamsPermit()
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

    [Fact]
    public async Task MultiDecideAllOnceReturnsPermitForEverySubscription()
    {
        await using var client = Client();

        var snapshot = await client.MultiDecideAllOnceAsync(TwoSubscriptions());

        snapshot.Decisions.Should().HaveCount(2)
            .And.OnlyContain(entry => entry.Value.Decision == Decision.Permit);
    }

    [Fact]
    public async Task MultiDecideStreamsAnIdentifiablePermitForEverySubscription()
    {
        await using var client = Client();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var seen = new Dictionary<string, Decision>();

        await foreach (var decision in client.MultiDecide(TwoSubscriptions(), cts.Token))
        {
            seen[decision.SubscriptionId] = decision.Decision.Decision;
            if (seen.Count == 2 && seen.Values.All(d => d == Decision.Permit))
            {
                break;
            }
        }

        seen.Should().HaveCount(2).And.OnlyContain(entry => entry.Value == Decision.Permit);
    }

    [Fact]
    public async Task MultiDecideAllStreamsASnapshotPermittingEverySubscription()
    {
        await using var client = Client();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        await foreach (var snapshot in client.MultiDecideAll(TwoSubscriptions(), cts.Token))
        {
            if (snapshot.Decisions.Count < 2 ||
                snapshot.Decisions.Values.Any(d => d.Decision != Decision.Permit))
            {
                continue;
            }

            snapshot.Decisions.Should().HaveCount(2)
                .And.OnlyContain(entry => entry.Value.Decision == Decision.Permit);
            return;
        }

        Assert.Fail("The multi-decide-all stream produced no fully-permitting snapshot.");
    }
}

[Trait("Category", "Integration")]
public sealed class RsocketPdpClientBasicAuthTests : IAsyncLifetime
{
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync() => _node = await SaplNode.StartAsync(new SaplNodeOptions
    {
        AllowNoAuth = false,
        AllowBasicAuth = true,
        Users = [new SaplNodeUser { Id = "it-basic-client", BasicUsername = "tester", BasicSecret = ItFixtures.CredentialHash }],
    });

    public async Task DisposeAsync() => await _node.DisposeAsync();

    [Fact]
    public async Task ValidCredentialsReturnPermit()
    {
        await using var client = new RsocketPdpClient(new RsocketPdpClientOptions
        {
            Host = _node.RsocketHost,
            Port = _node.RsocketPort,
            Username = "tester",
            Secret = ItFixtures.PlaintextCredential,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task WrongPasswordFailsClosedToIndeterminate()
    {
        await using var client = new RsocketPdpClient(new RsocketPdpClientOptions
        {
            Host = _node.RsocketHost,
            Port = _node.RsocketPort,
            Username = "tester",
            Secret = "wrong-password",
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Indeterminate);
    }
}

[Trait("Category", "Integration")]
public sealed class RsocketPdpClientApiKeyAuthTests : IAsyncLifetime
{
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync() => _node = await SaplNode.StartAsync(new SaplNodeOptions
    {
        AllowNoAuth = false,
        AllowApiKeyAuth = true,
        Users = [new SaplNodeUser { Id = "it-apikey-client", ApiKeyId = ItFixtures.ApiKeyId, ApiKey = ItFixtures.CredentialHash }],
    });

    public async Task DisposeAsync() => await _node.DisposeAsync();

    [Fact]
    public async Task ValidApiKeyReturnsPermit()
    {
        await using var client = new RsocketPdpClient(new RsocketPdpClientOptions
        {
            Host = _node.RsocketHost,
            Port = _node.RsocketPort,
            Token = ItFixtures.PlaintextCredential,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task MissingCredentialsFailClosedToIndeterminate()
    {
        await using var client = new RsocketPdpClient(new RsocketPdpClientOptions
        {
            Host = _node.RsocketHost,
            Port = _node.RsocketPort,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Indeterminate);
    }
}
