using DotNet.Testcontainers.Containers;

namespace Sapl.IntegrationTests;

/// <summary>A running SAPL Node container. Dispose to stop and remove it.</summary>
public sealed class StartedSaplNode(
    IContainer container,
    string httpUrl,
    string rsocketHost,
    int rsocketPort,
    string? caPemPath) : IAsyncDisposable
{
    /// <summary>HTTP or HTTPS base URL depending on the TLS option.</summary>
    public string HttpUrl { get; } = httpUrl;

    public string RsocketHost { get; } = rsocketHost;

    public int RsocketPort { get; } = rsocketPort;

    /// <summary>Path to the CA PEM trust anchor when TLS is enabled, else null.</summary>
    public string? CaPemPath { get; } = caPemPath;

    public async ValueTask DisposeAsync() => await container.DisposeAsync();
}
