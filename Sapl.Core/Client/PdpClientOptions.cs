using Sapl.Core.Client.Auth;

namespace Sapl.Core.Client;

public sealed record PdpClientOptions
{
    internal const string ErrorAuthBasicIncomplete = "Basic Auth requires both username and secret.";
    internal const string ErrorAuthDualConfig = "Configure at most one of Bearer token, Basic Auth, or a token provider.";
    internal const string ErrorBaseUrlEmpty = "PDP base URL must not be empty.";
    internal const string ErrorBaseUrlInvalid = "PDP base URL is not a valid URI: ";
    internal const string ErrorInsecureHttp = "PDP base URL uses plain HTTP to a non-loopback host. Use HTTPS, or run the PDP on localhost.";

    private const int DefaultTimeoutMs = 5000;
    private const int DefaultRetryBaseDelayMs = 1000;
    private const int DefaultRetryMaxDelayMs = 30000;

    public string BaseUrl { get; set; } = "https://localhost:8443";

    public string? Token { get; set; }

    public string? Username { get; set; }

    public string? Secret { get; set; }

    public int TimeoutMs { get; set; } = DefaultTimeoutMs;

    public int StreamingRetryBaseDelayMs { get; set; } = DefaultRetryBaseDelayMs;

    public int StreamingRetryMaxDelayMs { get; set; } = DefaultRetryMaxDelayMs;

    /// <summary>
    /// Resolves a rotating bearer token per request (e.g. OAuth2 client_credentials).
    /// Mutually exclusive with <see cref="Token"/> and Basic Auth.
    /// </summary>
    public IAccessTokenProvider? TokenProvider { get; set; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
        {
            throw new ArgumentException(ErrorBaseUrlEmpty);
        }

        if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException(ErrorBaseUrlInvalid + BaseUrl);
        }

        if (uri.Scheme == Uri.UriSchemeHttp && !uri.IsLoopback)
        {
            throw new ArgumentException(ErrorInsecureHttp);
        }

        var hasToken = !string.IsNullOrWhiteSpace(Token);
        var hasUsername = !string.IsNullOrWhiteSpace(Username);
        var hasSecret = !string.IsNullOrWhiteSpace(Secret);
        var hasProvider = TokenProvider is not null;

        var authSources = (hasToken ? 1 : 0) + (hasUsername || hasSecret ? 1 : 0) + (hasProvider ? 1 : 0);
        if (authSources > 1)
        {
            throw new ArgumentException(ErrorAuthDualConfig);
        }

        if (hasUsername != hasSecret)
        {
            throw new ArgumentException(ErrorAuthBasicIncomplete);
        }
    }
}
