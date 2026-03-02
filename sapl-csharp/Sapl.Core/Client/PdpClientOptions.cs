namespace Sapl.Core.Client;

public sealed record PdpClientOptions
{
    internal const string ErrorAuthBasicIncomplete = "Basic Auth requires both username and secret.";
    internal const string ErrorAuthDualConfig = "Cannot configure both Bearer token and Basic Auth simultaneously.";
    internal const string ErrorBaseUrlEmpty = "PDP base URL must not be empty.";
    internal const string ErrorBaseUrlInvalid = "PDP base URL is not a valid URI: ";
    internal const string ErrorInsecureHttp = "PDP base URL uses HTTP. Set AllowInsecureConnections = true to allow insecure connections.";

    private const int DefaultTimeoutMs = 5000;
    private const int DefaultRetryBaseDelayMs = 1000;
    private const int DefaultRetryMaxDelayMs = 30000;

    public string BaseUrl { get; set; } = "https://localhost:8443";

    public string? Token { get; set; }

    public string? Username { get; set; }

    public string? Secret { get; set; }

    public int TimeoutMs { get; set; } = DefaultTimeoutMs;

    public int StreamingMaxRetries { get; set; }

    public int StreamingRetryBaseDelayMs { get; set; } = DefaultRetryBaseDelayMs;

    public int StreamingRetryMaxDelayMs { get; set; } = DefaultRetryMaxDelayMs;

    public bool AllowInsecureConnections { get; set; }

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

        if (uri.Scheme == Uri.UriSchemeHttp && !AllowInsecureConnections)
        {
            throw new ArgumentException(ErrorInsecureHttp);
        }

        var hasToken = !string.IsNullOrWhiteSpace(Token);
        var hasUsername = !string.IsNullOrWhiteSpace(Username);
        var hasSecret = !string.IsNullOrWhiteSpace(Secret);

        if (hasToken && (hasUsername || hasSecret))
        {
            throw new ArgumentException(ErrorAuthDualConfig);
        }

        if (hasUsername != hasSecret)
        {
            throw new ArgumentException(ErrorAuthBasicIncomplete);
        }
    }
}
