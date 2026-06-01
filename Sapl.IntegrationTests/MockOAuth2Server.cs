using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Networks;

namespace Sapl.IntegrationTests;

/// <summary>A running Navikt mock-oauth2-server. Dispose to stop and remove it.</summary>
public sealed class StartedMockOAuth2Server(IContainer container, string issuerUri, string hostIssuerUri)
    : IAsyncDisposable
{
    /// <summary>Issuer URI as the SAPL Node sees it, over the shared network alias.</summary>
    public string IssuerUri { get; } = issuerUri;

    /// <summary>Issuer URI as host code sees it, over the mapped port.</summary>
    public string HostIssuerUri { get; } = hostIssuerUri;

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}

/// <summary>
/// Starts a mock-oauth2-server: a lightweight JWT issuer that mints a token for
/// any client_credentials request and exposes the matching JWKS. The hostname is
/// left unset so discovery URLs follow the request host (host code reaches it over
/// the mapped port, the node over the network alias), while the iss claim is pinned
/// to the alias issuer so the node accepts tokens regardless of which host minted them.
/// </summary>
public static class MockOAuth2Server
{
    private const string Image = "ghcr.io/navikt/mock-oauth2-server:2.1.0";
    private const string Alias = "auth-host";
    private const string IssuerId = "default";
    private const ushort Port = 8080;

    public static async Task<StartedMockOAuth2Server> StartAsync(INetwork network)
    {
        var issuerUri = $"http://{Alias}:{Port}/{IssuerId}";
        var jsonConfig = JsonSerializer.Serialize(new
        {
            interactiveLogin = false,
            tokenCallbacks = new[]
            {
                new
                {
                    issuerId = IssuerId,
                    requestMappings = new[]
                    {
                        new
                        {
                            requestParam = "grant_type",
                            match = "client_credentials",
                            claims = new { sub = "sapl-client", iss = issuerUri },
                        },
                    },
                },
            },
        });

        var container = new ContainerBuilder(Image)
            .WithNetwork(network)
            .WithNetworkAliases(Alias)
            .WithPortBinding(Port, true)
            .WithEnvironment("JSON_CONFIG", jsonConfig)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request
                .ForPath($"/{IssuerId}/.well-known/openid-configuration")
                .ForPort(Port)
                .ForStatusCode(HttpStatusCode.OK)))
            .Build();
        await container.StartAsync();

        var hostIssuerUri = $"http://{container.Hostname}:{container.GetMappedPublicPort(Port)}/{IssuerId}";
        return new StartedMockOAuth2Server(container, issuerUri, hostIssuerUri);
    }
}
