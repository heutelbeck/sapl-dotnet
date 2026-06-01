namespace Sapl.Core.Client.Auth;

public sealed record OAuth2TokenProviderOptions
{
    /// <summary>OIDC issuer URL. The provider discovers the token endpoint from it.</summary>
    public required string IssuerUrl { get; init; }

    public required string ClientId { get; init; }

    public required string ClientSecret { get; init; }

    /// <summary>Optional space-separated scopes to request.</summary>
    public string? Scope { get; init; }

    /// <summary>Refresh a cached token once it falls within this window of its expiry.</summary>
    public TimeSpan RefreshGuard { get; init; } = TimeSpan.FromSeconds(30);
}
