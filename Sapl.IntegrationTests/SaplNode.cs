using System.Text;
using DotNet.Testcontainers.Builders;

namespace Sapl.IntegrationTests;

/// <summary>
/// Starts a fresh SAPL Node container with a permit-all policy bundle, routed to
/// one node process over HTTP(S) and RSocket. The caller disposes the result.
/// </summary>
public static class SaplNode
{
    private const string DefaultImage = "ghcr.io/heutelbeck/sapl-node:4.1.0-SNAPSHOT";
    private const string ReadyLog = "SAPL Node ready";
    private const string TlsBundle = "saplbundle";
    private const int HttpPort = 8080;
    private const int HttpsPort = 8443;
    private const int RsocketPort = 7000;

    private const string PermitAllPolicy = "policy \"permit-all\"\npermit";

    private const string ReadinessProbeBody = """{"subject":"_","action":"_","resource":"_"}""";

    private const string PdpConfigJson =
        """{"algorithm":{"votingMode":"PRIORITY_PERMIT","defaultDecision":"DENY","errorHandling":"ABSTAIN"},"variables":{}}""";

    private static readonly string TlsFixtureDir = Path.Combine(AppContext.BaseDirectory, "fixtures", "tls");

    public static async Task<StartedSaplNode> StartAsync(SaplNodeOptions options)
    {
        var httpPort = options.Tls ? HttpsPort : HttpPort;

        var builder = new ContainerBuilder(options.Image ?? DefaultImage)
            .WithPortBinding(httpPort, true)
            .WithPortBinding(RsocketPort, true)
            .WithResourceMapping(Encoding.UTF8.GetBytes(PermitAllPolicy), "/pdp/data/permit-all.sapl")
            .WithResourceMapping(Encoding.UTF8.GetBytes(PdpConfigJson), "/pdp/data/pdp.json")
            .WithEnvironment(BuildEnvironment(options, httpPort))
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(ReadyLog));

        if (options.Tls)
        {
            builder = builder
                .WithResourceMapping(File.ReadAllBytes(Path.Combine(TlsFixtureDir, "server.pem")), "/pdp/data/server.pem")
                .WithResourceMapping(File.ReadAllBytes(Path.Combine(TlsFixtureDir, "server.key")), "/pdp/data/server.key");
        }

        if (options.Network is not null)
        {
            builder = builder.WithNetwork(options.Network).WithNetworkAliases("sapl-node");
        }

        var container = builder.Build();
        await container.StartAsync();

        var host = container.Hostname;
        var scheme = options.Tls ? "https" : "http";
        var httpUrl = $"{scheme}://{host}:{container.GetMappedPublicPort(httpPort)}";
        var caPem = options.Tls ? Path.Combine(TlsFixtureDir, "ca.pem") : null;

        await WaitUntilDecisionReadyAsync(httpUrl, caPem, TimeSpan.FromSeconds(30));

        return new StartedSaplNode(container, httpUrl, host, container.GetMappedPublicPort(RsocketPort), caPem);
    }

    private static Dictionary<string, string> BuildEnvironment(SaplNodeOptions options, int httpPort)
    {
        var env = new Dictionary<string, string>
        {
            ["IO_SAPL_PDP_EMBEDDED_PDPCONFIGTYPE"] = "DIRECTORY",
            ["IO_SAPL_PDP_EMBEDDED_CONFIGPATH"] = "/pdp/data",
            ["IO_SAPL_PDP_EMBEDDED_POLICIESPATH"] = "/pdp/data",
            ["IO_SAPL_NODE_ALLOWNOAUTH"] = Bool(options.AllowNoAuth),
            ["IO_SAPL_NODE_ALLOWBASICAUTH"] = Bool(options.AllowBasicAuth),
            ["IO_SAPL_NODE_ALLOWAPIKEYAUTH"] = Bool(options.AllowApiKeyAuth),
            ["IO_SAPL_NODE_ALLOWOAUTH2AUTH"] = Bool(options.AllowOAuth2),
        };

        if (options.OAuth2IssuerUri is not null)
        {
            env["SPRING_SECURITY_OAUTH2_RESOURCESERVER_JWT_ISSUERURI"] = options.OAuth2IssuerUri;
        }

        if (options.Tls)
        {
            env[$"SPRING_SSL_BUNDLE_PEM_{TlsBundle.ToUpperInvariant()}_KEYSTORE_CERTIFICATE"] = "/pdp/data/server.pem";
            env[$"SPRING_SSL_BUNDLE_PEM_{TlsBundle.ToUpperInvariant()}_KEYSTORE_PRIVATEKEY"] = "/pdp/data/server.key";
            env["SERVER_SSL_BUNDLE"] = TlsBundle;
            env["SERVER_SSL_ENABLED"] = "true";
            env["SERVER_PORT"] = httpPort.ToString();
            env["SAPL_PDP_RSOCKET_SSL_BUNDLE"] = TlsBundle;
        }

        for (var index = 0; index < options.Users.Count; index++)
        {
            var user = options.Users[index];
            var prefix = $"IO_SAPL_NODE_USERS_{index}_";
            env[prefix + "ID"] = user.Id;
            if (user.BasicUsername is not null)
            {
                env[prefix + "BASIC_USERNAME"] = user.BasicUsername;
            }

            if (user.BasicSecret is not null)
            {
                env[prefix + "BASIC_SECRET"] = user.BasicSecret;
            }

            if (user.ApiKey is not null)
            {
                env[prefix + "APIKEY"] = user.ApiKey;
            }

            if (user.ApiKeyId is not null)
            {
                env[prefix + "APIKEYID"] = user.ApiKeyId;
            }
        }

        return env;
    }

    private static string Bool(bool value) => value ? "true" : "false";

    /// <summary>
    /// Waits until the node answers an HTTP decision request, mirroring the readiness
    /// probe the other PEP integration suites use. The "SAPL Node ready" log marks process
    /// startup, not decision-readiness; without this a cold PDP can make the first
    /// multi-decision snapshot time out, since xUnit runs the facts in arbitrary order.
    /// </summary>
    private static async Task WaitUntilDecisionReadyAsync(string httpUrl, string? caPemPath, TimeSpan timeout)
    {
        using var client = caPemPath is null
            ? new HttpClient()
            : new HttpClient(Tls.TrustingHandler(caPemPath), disposeHandler: true);
        client.Timeout = TimeSpan.FromSeconds(2);

        var url = $"{httpUrl}/api/pdp/decide-once";
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                using var content = new StringContent(ReadinessProbeBody, Encoding.UTF8, "application/json");
                using (await client.PostAsync(url, content))
                {
                    return;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500));
            }
        }

        throw new TimeoutException($"SAPL Node did not become decision-ready within {timeout.TotalSeconds:0}s.");
    }
}
