using Sapl.Core.Client.Auth;

namespace Sapl.Rsocket;

/// <summary>TLS settings for the RSocket transport. The CA is trusted as a custom root.</summary>
public sealed record RsocketTlsOptions
{
    /// <summary>Path to a PEM CA file trusted as the connection's root anchor.</summary>
    public required string CaPemPath { get; init; }

    /// <summary>Server name for SNI and hostname validation. Defaults to the host.</summary>
    public string? ServerName { get; init; }
}

public sealed record RsocketPdpClientOptions
{
    internal const string ErrorAuthConflict = "Configure at most one of Bearer token, Basic Auth, or a token provider.";
    internal const string ErrorBasicIncomplete = "Basic Auth requires both username and secret.";
    internal const string ErrorHostRequired = "RSocket client requires a non-empty host.";
    internal const string ErrorPlaintextNonLoopback = "RSocket refuses plaintext to a non-loopback host. Configure TLS or use localhost.";
    internal const string ErrorPortRequired = "RSocket client requires a positive port.";

    private static readonly HashSet<string> LoopbackHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "127.0.0.1", "::1",
    };

    public required string Host { get; init; }

    public required int Port { get; init; }

    /// <summary>Bearer token: a SAPL API key (sapl_ prefix) or a static JWT.</summary>
    public string? Token { get; init; }

    public string? Username { get; init; }

    public string? Secret { get; init; }

    /// <summary>Rotating bearer token (e.g. OAuth2 client_credentials). Resolved per connection.</summary>
    public IAccessTokenProvider? TokenProvider { get; init; }

    public RsocketTlsOptions? Tls { get; init; }

    /// <summary>Base delay (ms) for the streaming reconnect backoff.</summary>
    public int StreamingRetryBaseDelayMs { get; init; } = 1000;

    /// <summary>Maximum delay (ms) the streaming reconnect backoff caps at.</summary>
    public int StreamingRetryMaxDelayMs { get; init; } = 30000;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException(ErrorHostRequired);
        }

        if (Port <= 0)
        {
            throw new ArgumentException(ErrorPortRequired);
        }

        var hasToken = !string.IsNullOrWhiteSpace(Token);
        var hasUsername = !string.IsNullOrWhiteSpace(Username);
        var hasSecret = !string.IsNullOrWhiteSpace(Secret);
        var hasProvider = TokenProvider is not null;

        var authSources = (hasToken ? 1 : 0) + (hasUsername || hasSecret ? 1 : 0) + (hasProvider ? 1 : 0);
        if (authSources > 1)
        {
            throw new ArgumentException(ErrorAuthConflict);
        }

        if (hasUsername != hasSecret)
        {
            throw new ArgumentException(ErrorBasicIncomplete);
        }

        if (Tls is null && !LoopbackHosts.Contains(Host))
        {
            throw new ArgumentException(ErrorPlaintextNonLoopback);
        }
    }
}
