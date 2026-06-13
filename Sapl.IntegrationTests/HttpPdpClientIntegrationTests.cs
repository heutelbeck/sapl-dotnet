using FluentAssertions;
using Sapl.Core.Authorization;
using Sapl.Core.Client;

namespace Sapl.IntegrationTests;

[Trait("Category", "Integration")]
public sealed class HttpPdpClientNoAuthTests : IAsyncLifetime
{
    private StartedSaplNode _node = null!;

    public async Task InitializeAsync() => _node = await SaplNode.StartAsync(new SaplNodeOptions());

    public async Task DisposeAsync() => await _node.DisposeAsync();

    private PdpClient Client() => PdpClients.Create(new PdpClientOptions
    {
        BaseUrl = _node.HttpUrl,
    });

    [Fact]
    public async Task DecideOnceReturnsPermit()
    {
        var decision = await Client().DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task DecideStreamsPermit()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await foreach (var decision in Client().Decide(ItFixtures.PermitSubscription, cts.Token))
        {
            decision.Decision.Should().Be(Decision.Permit);
            return;
        }

        Assert.Fail("The decision stream produced no decision.");
    }

    [Fact]
    public async Task MultiDecideAllOnceReturnsPermitForEverySubscription()
    {
        var snapshot = await Client().MultiDecideAllOnceAsync(TwoSubscriptions());

        snapshot.Decisions.Should().HaveCount(2)
            .And.OnlyContain(entry => entry.Value.Decision == Decision.Permit);
    }

    [Fact]
    public async Task MultiDecideStreamsAnIdentifiablePermitForEverySubscription()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var seen = new Dictionary<string, Decision>();

        await foreach (var decision in Client().MultiDecide(TwoSubscriptions(), cts.Token))
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
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        await foreach (var snapshot in Client().MultiDecideAll(TwoSubscriptions(), cts.Token))
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

    private static MultiAuthorizationSubscription TwoSubscriptions() => new()
    {
        Subscriptions = new Dictionary<string, AuthorizationSubscription>
        {
            ["a"] = AuthorizationSubscription.Create("alice", "read", "doc-1"),
            ["b"] = AuthorizationSubscription.Create("alice", "write", "doc-2"),
        },
    };
}

[Trait("Category", "Integration")]
public sealed class HttpPdpClientBasicAuthTests : IAsyncLifetime
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
        var client = PdpClients.Create(new PdpClientOptions
        {
            BaseUrl = _node.HttpUrl,
            Username = "tester",
            Secret = ItFixtures.PlaintextCredential,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task WrongPasswordFailsClosedToIndeterminate()
    {
        var client = PdpClients.Create(new PdpClientOptions
        {
            BaseUrl = _node.HttpUrl,
            Username = "tester",
            Secret = "wrong-password",
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Indeterminate);
    }
}

[Trait("Category", "Integration")]
public sealed class HttpPdpClientApiKeyAuthTests : IAsyncLifetime
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
        var client = PdpClients.Create(new PdpClientOptions
        {
            BaseUrl = _node.HttpUrl,
            Token = ItFixtures.PlaintextCredential,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Permit);
    }

    [Fact]
    public async Task MissingCredentialsFailClosedToIndeterminate()
    {
        var client = PdpClients.Create(new PdpClientOptions
        {
            BaseUrl = _node.HttpUrl,
        });

        var decision = await client.DecideOnceAsync(ItFixtures.PermitSubscription);

        decision.Decision.Should().Be(Decision.Indeterminate);
    }
}
