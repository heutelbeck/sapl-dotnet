using DotNet.Testcontainers.Networks;

namespace Sapl.IntegrationTests;

/// <summary>
/// Container configuration for a test SAPL Node. Defaults to no-auth, plaintext
/// HTTP. These defaults are for fast local IT bring-up only, never production.
/// </summary>
public sealed record SaplNodeOptions
{
    public string? Image { get; init; }

    public bool AllowNoAuth { get; init; } = true;

    public bool AllowBasicAuth { get; init; }

    public bool AllowApiKeyAuth { get; init; }

    public bool AllowOAuth2 { get; init; }

    /// <summary>Issuer URI the node uses to fetch JWKS and validate JWTs.</summary>
    public string? OAuth2IssuerUri { get; init; }

    public IReadOnlyList<SaplNodeUser> Users { get; init; } = [];

    /// <summary>Serve HTTPS on 8443 and TLS on the RSocket port from the fixture cert pair.</summary>
    public bool Tls { get; init; }

    /// <summary>Shared network so the node can resolve a sibling container hostname.</summary>
    public INetwork? Network { get; init; }
}

public sealed record SaplNodeUser
{
    public required string Id { get; init; }

    public string? BasicUsername { get; init; }

    public string? BasicSecret { get; init; }

    public string? ApiKey { get; init; }

    public string? ApiKeyId { get; init; }
}
