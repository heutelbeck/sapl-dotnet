using System.Text.Json;

namespace Sapl.Core.Client.Auth;

/// <summary>
/// Acquires and caches OAuth2 access tokens via the client_credentials grant. A
/// cached token is reused until it falls within the refresh guard of its expiry,
/// then the next call triggers a fresh grant. Concurrent callers share a single
/// in-flight refresh.
/// </summary>
public sealed class OAuth2TokenProvider : IAccessTokenProvider
{
    internal const string ErrorMissingAccessToken =
        "OAuth2 client_credentials response did not include an access_token.";

    internal const string ErrorMissingTokenEndpoint =
        "OAuth2 issuer discovery document did not include a token_endpoint.";

    private const int DefaultLifetimeSeconds = 60;

    private readonly OAuth2TokenProviderOptions _options;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile CachedToken? _cached;
    private string? _tokenEndpoint;

    public OAuth2TokenProvider(OAuth2TokenProviderOptions options, HttpClient httpClient)
    {
        _options = options;
        _httpClient = httpClient;
    }

    public async ValueTask<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var cached = _cached;
        if (cached is not null && !IsExpiring(cached))
        {
            return cached.AccessToken;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            cached = _cached;
            if (cached is not null && !IsExpiring(cached))
            {
                return cached.AccessToken;
            }

            return await RefreshAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate() => _cached = null;

    private bool IsExpiring(CachedToken token) =>
        token.ExpiresAt <= DateTimeOffset.UtcNow + _options.RefreshGuard;

    private async ValueTask<string> RefreshAsync(CancellationToken cancellationToken)
    {
        var tokenEndpoint = await ResolveTokenEndpointAsync(cancellationToken).ConfigureAwait(false);
        using var content = new FormUrlEncodedContent(BuildGrantParameters());
        using var response = await _httpClient.PostAsync(tokenEndpoint, content, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (!root.TryGetProperty("access_token", out var accessToken) ||
            accessToken.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(ErrorMissingAccessToken);
        }

        var lifetimeSeconds = root.TryGetProperty("expires_in", out var expiresIn) && expiresIn.TryGetInt32(out var seconds)
            ? seconds
            : DefaultLifetimeSeconds;
        var token = accessToken.GetString()!;
        _cached = new CachedToken(token, DateTimeOffset.UtcNow + TimeSpan.FromSeconds(lifetimeSeconds));
        return token;
    }

    private async ValueTask<string> ResolveTokenEndpointAsync(CancellationToken cancellationToken)
    {
        if (_tokenEndpoint is not null)
        {
            return _tokenEndpoint;
        }

        var discoveryUrl = _options.IssuerUrl.TrimEnd('/') + "/.well-known/openid-configuration";
        using var response = await _httpClient.GetAsync(discoveryUrl, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("token_endpoint", out var endpoint) ||
            endpoint.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException(ErrorMissingTokenEndpoint);
        }

        _tokenEndpoint = endpoint.GetString()!;
        return _tokenEndpoint;
    }

    private List<KeyValuePair<string, string>> BuildGrantParameters()
    {
        var parameters = new List<KeyValuePair<string, string>>
        {
            new("grant_type", "client_credentials"),
            new("client_id", _options.ClientId),
            new("client_secret", _options.ClientSecret),
        };
        if (!string.IsNullOrWhiteSpace(_options.Scope))
        {
            parameters.Add(new("scope", _options.Scope));
        }

        return parameters;
    }

    private sealed record CachedToken(string AccessToken, DateTimeOffset ExpiresAt);
}
